using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Rag;

public record RewriteResult(string Question, bool Rewritten, ChatUsage? Usage);

public class QuestionRewriter(
    IChatCompletionService chat,
    PromptBuilder promptBuilder,
    IOptions<RagOptions> options,
    ILogger<QuestionRewriter> logger)
{
    /// <summary>Rewrites a follow-up into a standalone question. Skipped without history; falls back to the
    /// original question on any failure (logged without content).</summary>
    public async Task<RewriteResult> RewriteAsync(string question, IReadOnlyList<ConversationTurnDto> history, CancellationToken cancellationToken)
    {
        if (history.Count == 0)
        {
            return new RewriteResult(question, false, null);
        }

        try
        {
            var result = await chat.CompleteAsync(promptBuilder.BuildRewriteMessages(history, question), cancellationToken);
            var rewritten = Clean(result.Text, options.Value.MaxQuestionLength);
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                return new RewriteResult(question, false, result.Usage);
            }

            return new RewriteResult(rewritten, !string.Equals(rewritten, question, StringComparison.Ordinal), result.Usage);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("Question rewrite failed ({ExceptionType}); using the original question", ex.GetType().Name);
            return new RewriteResult(question, false, null);
        }
    }

    private static string Clean(string text, int maxLength)
    {
        var t = text.Trim().Trim('"', '“', '”').Trim();
        return t.Length > maxLength ? t[..maxLength] : t;
    }
}
