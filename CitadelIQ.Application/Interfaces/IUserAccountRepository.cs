using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

/// <summary>Display data for "uploaded by". <see cref="WorkspaceId"/> is the user's current workspace, if any.</summary>
public record UserDisplayInfo(Guid UserId, string DisplayName, bool IsClosed, Guid? WorkspaceId);

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Adds the user, or — if a concurrent first sign-in already created one with the same identity —
    /// returns that existing user instead.</summary>
    Task<UserAccount> AddOrGetAsync(UserAccount user, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default);
}
