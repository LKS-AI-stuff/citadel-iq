using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>The email agent's own model calls (triage, reworded query, judge). Counts calls and tokens for the log
/// line; never logs prompts or replies.</summary>
public class EmailModelClient(IChatCompletionService chat, IOptions<RagOptions> ragOptions, ILogger<EmailModelClient> logger)
{
    public int Calls { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }

    /// <summary>One retry on invalid JSON, then an <see cref="AnswerGenerationException"/> (a transient failure).</summary>
    public async Task<TriageResult> TriageAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var messages = EmailPromptBuilder.Triage(subject, body);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var parsed = EmailPromptBuilder.ParseTriage(await CompleteAsync(messages, cancellationToken), ragOptions.Value.MaxQuestionLength);
            if (parsed is not null)
            {
                return parsed;
            }

            logger.LogWarning("Email triage returned invalid JSON (attempt {Attempt})", attempt);
        }

        throw new AnswerGenerationException("The email could not be classified.");
    }

    /// <summary>An alternative search query; null when the call fails or returns nothing (the first retrieval stands).</summary>
    public async Task<string?> RewordAsync(string question, CancellationToken cancellationToken)
    {
        try
        {
            var query = EmailPromptBuilder.CleanQuery(await CompleteAsync(EmailPromptBuilder.Reword(question), cancellationToken), ragOptions.Value.MaxQuestionLength);
            return query.Length == 0 ? null : query;
        }
        catch (AnswerGenerationException)
        {
            logger.LogWarning("Email reworded query failed; using the first retrieval");
            return null;
        }
    }

    /// <summary>Invalid JSON counts as confidence 0 (so the email becomes a draft); provider failures propagate.</summary>
    public async Task<JudgeResult> JudgeAsync(string question, IReadOnlyList<AnswerSourceDto> sources, string answer, CancellationToken cancellationToken)
    {
        var parsed = EmailPromptBuilder.ParseJudge(await CompleteAsync(EmailPromptBuilder.Judge(question, sources, answer), cancellationToken));
        if (parsed is null)
        {
            logger.LogWarning("Email judge returned invalid JSON; treating confidence as 0");
        }

        return parsed ?? new JudgeResult(0, null);
    }

    private async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        Calls++;
        var result = await chat.CompleteAsync(messages, cancellationToken);
        InputTokens += result.Usage?.InputTokens ?? 0;
        OutputTokens += result.Usage?.OutputTokens ?? 0;
        return result.Text;
    }
}
