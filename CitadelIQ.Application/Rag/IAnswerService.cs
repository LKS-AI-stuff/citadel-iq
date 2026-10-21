using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Rag;

/// <summary>
/// Optional retrieval settings for one answer (the email agent's reworded-query retry; Ask passes none).
/// <see cref="AdditionalQueries"/> are searched after the question itself and merged by best similarity per chunk.
/// <see cref="QuestionIsStandalone"/> skips the follow-up rewrite when the caller already has a standalone question.
/// </summary>
public record AnswerRetrieval(IReadOnlyList<string> AdditionalQueries, bool QuestionIsStandalone = false);

public interface IAnswerService
{
    /// <summary>Validates, rewrites (if there is history), retrieves and selects context. Throws normal exceptions
    /// (<c>ValidationException</c>, <c>NotFoundException</c>, <c>FeatureDisabledException</c>) — nothing has been streamed yet.</summary>
    Task<AnswerRun> StartAsync(AskRequestDto request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="StartAsync(AskRequestDto, CancellationToken)"/>
    Task<AnswerRun> StartAsync(AskRequestDto request, AnswerRetrieval? retrieval, CancellationToken cancellationToken = default);
}
