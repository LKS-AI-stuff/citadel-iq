using System.ClientModel;
using System.Runtime.CompilerServices;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using AppChatMessage = CitadelIQ.Application.Interfaces.ChatMessage;

namespace CitadelIQ.Infrastructure.AI;

public class OpenAIChatCompletionService(
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<RagOptions> ragOptions,
    ILogger<OpenAIChatCompletionService> logger) : IChatCompletionService
{
    private const string RateLimitedMessage = "The AI service is busy right now. Please try again in a moment.";
    private const string UnavailableMessage = "The AI service is currently unavailable. Please try again.";

    private readonly Lazy<ChatClient> _client = new(() =>
    {
        var o = openAiOptions.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.ChatModel))
        {
            throw new InvalidOperationException("OpenAI API key or chat model is not configured.");
        }

        return new ChatClient(o.ChatModel, o.ApiKey);
    });

    public async Task<ChatResult> CompleteAsync(IReadOnlyList<AppChatMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var completion = (await _client.Value.CompleteChatAsync(ToSdk(messages), BuildOptions(), cancellationToken)).Value;
            var text = string.Concat(completion.Content.Where(p => p.Kind == ChatMessageContentPartKind.Text).Select(p => p.Text));
            return new ChatResult(text, completion.Usage is null ? null : new ChatUsage(completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount));
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            throw Map(ex);
        }
    }

    public async IAsyncEnumerable<ChatStreamItem> StreamAsync(
        IReadOnlyList<AppChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var updates = _client.Value.CompleteChatStreamingAsync(ToSdk(messages), BuildOptions(), cancellationToken);
        var enumerator = updates.GetAsyncEnumerator(cancellationToken);
        ChatUsage? usage = null;

        try
        {
            while (true)
            {
                StreamingChatCompletionUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
                {
                    throw Map(ex);
                }

                if (update.Usage is not null)
                {
                    usage = new ChatUsage(update.Usage.InputTokenCount, update.Usage.OutputTokenCount);
                }

                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        yield return new ChatTextDelta(part.Text);
                    }
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        yield return new ChatStreamCompleted(usage);
    }

    private ChatCompletionOptions BuildOptions()
    {
        var options = new ChatCompletionOptions();
        var rag = ragOptions.Value;
        if (rag.Temperature is { } temperature)
        {
            options.Temperature = (float)temperature;
        }

        if (rag.MaxOutputTokens is { } max)
        {
            options.MaxOutputTokenCount = max;
        }

        return options;
    }

    private static List<OpenAI.Chat.ChatMessage> ToSdk(IReadOnlyList<AppChatMessage> messages) =>
        messages.Select(m => m.Role switch
        {
            ChatRole.System => (OpenAI.Chat.ChatMessage)new SystemChatMessage(m.Content),
            ChatRole.Assistant => new AssistantChatMessage(m.Content),
            _ => new UserChatMessage(m.Content)
        }).ToList();

    private AnswerGenerationException Map(Exception ex)
    {
        if (ex is ClientResultException cre)
        {
            logger.LogError("Chat completion failed with status {Status}", cre.Status);
            return new AnswerGenerationException(cre.Status == 429 ? RateLimitedMessage : UnavailableMessage, ex);
        }

        logger.LogError("Chat completion failed ({ExceptionType})", ex.GetType().Name);
        return new AnswerGenerationException(UnavailableMessage, ex);
    }
}
