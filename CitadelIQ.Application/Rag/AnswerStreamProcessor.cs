using System.Runtime.CompilerServices;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Application.Rag;

/// <summary>
/// Turns the raw model stream into answer events: detects the <c>NOT_FOUND</c> refusal sentinel at the start of
/// the reply (buffering only until it can be decided, then cancelling the upstream stream), passes text through,
/// and finishes with the validated citations.
/// </summary>
public class AnswerStreamProcessor
{
    public async IAsyncEnumerable<AnswerEvent> ProcessAsync(
        IAsyncEnumerable<ChatStreamItem> stream,
        int sourceCount,
        ChatUsage? priorUsage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var full = new System.Text.StringBuilder();
        var pending = new System.Text.StringBuilder();
        var deciding = true;
        var notFound = false;
        ChatUsage? answerUsage = null;

        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            if (item is ChatStreamCompleted completed)
            {
                answerUsage = completed.Usage;
                continue;
            }

            if (item is not ChatTextDelta { Text.Length: > 0 } delta)
            {
                continue;
            }

            if (!deciding)
            {
                full.Append(delta.Text);
                yield return new AnswerTextEvent(delta.Text);
                continue;
            }

            pending.Append(delta.Text);
            var trimmed = pending.ToString().TrimStart();

            if (trimmed.Length == 0 || PromptBuilder.NotFoundSentinel.StartsWith(trimmed, StringComparison.Ordinal) && trimmed.Length < PromptBuilder.NotFoundSentinel.Length)
            {
                continue; // can't decide yet
            }

            deciding = false;
            if (trimmed.StartsWith(PromptBuilder.NotFoundSentinel, StringComparison.Ordinal))
            {
                notFound = true;
                break; // leaving the loop disposes the upstream enumerator, cancelling generation
            }

            full.Append(pending);
            yield return new AnswerTextEvent(pending.ToString());
            pending.Clear();
        }

        if (notFound)
        {
            yield return new AnswerNotFoundEvent();
            yield return new AnswerDoneEvent([], false, ToDto(priorUsage, null));
            yield break;
        }

        if (deciding && pending.Length > 0)
        {
            // Stream ended on a partial sentinel like "NOT" — it was ordinary text after all.
            full.Append(pending);
            yield return new AnswerTextEvent(pending.ToString());
        }

        var cited = CitationParser.Extract(full.ToString(), sourceCount);
        yield return new AnswerDoneEvent(cited, cited.Count > 0, ToDto(priorUsage, answerUsage));
    }

    internal static AnswerUsageDto? ToDto(ChatUsage? a, ChatUsage? b)
    {
        if (a is null && b is null)
        {
            return null;
        }

        return new AnswerUsageDto((a?.InputTokens ?? 0) + (b?.InputTokens ?? 0), (a?.OutputTokens ?? 0) + (b?.OutputTokens ?? 0));
    }
}
