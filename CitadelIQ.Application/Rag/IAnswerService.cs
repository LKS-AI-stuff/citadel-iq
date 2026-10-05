using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Rag;

public interface IAnswerService
{
    /// <summary>Validates, rewrites (if there is history), retrieves and selects context. Throws normal exceptions
    /// (<c>ValidationException</c>, <c>NotFoundException</c>, <c>FeatureDisabledException</c>) — nothing has been streamed yet.</summary>
    Task<AnswerRun> StartAsync(AskRequestDto request, CancellationToken cancellationToken = default);
}
