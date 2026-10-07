using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public record JoinRequestRecord(JoinRequest Request, UserAccount User);

public interface IJoinRequestRepository
{
    Task<JoinRequest?> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's most recent request, whatever its status.</summary>
    Task<JoinRequest?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JoinRequestRecord>> ListPendingAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Adds a pending request; a user who already has one gets a <c>ValidationException</c>.</summary>
    Task AddAsync(JoinRequest request, CancellationToken cancellationToken = default);
}
