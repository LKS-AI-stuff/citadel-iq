using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Application.Rag;

/// <summary>A prepared answer: the standalone question and numbered sources are known; the text is streamed on demand.</summary>
public sealed class AnswerRun
{
    private readonly IChatCompletionService _chat;
    private readonly PromptBuilder _promptBuilder;
    private readonly AnswerStreamProcessor _processor;
    private readonly IReadOnlyList<ConversationTurnDto> _history;
    private readonly ChatUsage? _rewriteUsage;
    private readonly ILogger _logger;

    internal AnswerRun(
        string standaloneQuestion,
        bool rewritten,
        IReadOnlyList<AnswerSourceDto> sources,
        IReadOnlyList<ConversationTurnDto> history,
        ChatUsage? rewriteUsage,
        IChatCompletionService chat,
        PromptBuilder promptBuilder,
        AnswerStreamProcessor processor,
        ILogger logger)
    {
        StandaloneQuestion = standaloneQuestion;
        Rewritten = rewritten;
        Sources = sources;
        _history = history;
        _rewriteUsage = rewriteUsage;
        _chat = chat;
        _promptBuilder = promptBuilder;
        _processor = processor;
        _logger = logger;
    }

    public string StandaloneQuestion { get; }

    public bool Rewritten { get; }

    public IReadOnlyList<AnswerSourceDto> Sources { get; }

    /// <summary>Yields text / notfound / done. With no sources it yields NotFound + Done and never calls the model.</summary>
    public async IAsyncEnumerable<AnswerEvent> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var outcome = "streamed";

        if (Sources.Count == 0)
        {
            _logger.LogInformation("Answer finished: outcome notfound (no sources), no model call");
            yield return new AnswerNotFoundEvent();
            yield return new AnswerDoneEvent([], false, AnswerStreamProcessor.ToDto(_rewriteUsage, null));
            yield break;
        }

        var messages = _promptBuilder.BuildAnswerMessages(_history, StandaloneQuestion, Sources);
        var events = _processor.ProcessAsync(_chat.StreamAsync(messages, cancellationToken), Sources.Count, _rewriteUsage, cancellationToken);

        await using var enumerator = events.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    yield break;
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (AnswerGenerationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Answer generation failed ({ExceptionType})", ex.GetType().Name);
                throw new AnswerGenerationException("An error occurred while generating the answer.", ex);
            }

            var current = enumerator.Current;
            if (current is AnswerNotFoundEvent) outcome = "notfound";
            if (current is AnswerDoneEvent done)
            {
                if (outcome == "streamed") outcome = done.Verified ? "answered" : "unverified";
                _logger.LogInformation(
                    "Answer finished: outcome {Outcome}, input tokens {InputTokens}, output tokens {OutputTokens}, {ElapsedMs} ms",
                    outcome, done.Usage?.InputTokens, done.Usage?.OutputTokens, stopwatch.ElapsedMilliseconds);
            }

            yield return current;
        }
    }
}
