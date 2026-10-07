using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Accounts;

public record ResolvedUser(UserAccount User, Membership? Membership, Workspace? Workspace);

public interface IAccountService
{
    /// <summary>Creates the user on first sign-in, otherwise refreshes email/display name from the identity
    /// provider's claims (the source of truth for both).</summary>
    Task<UserAccount> EnsureUserAsync(string issuer, string subject, string? email, string? displayName, CancellationToken cancellationToken = default);

    /// <summary>Loads the user, membership and workspace for an identity; null if the user is unknown.</summary>
    Task<ResolvedUser?> ResolveAsync(string issuer, string subject, CancellationToken cancellationToken = default);

    /// <summary>The signed-in user's session state (<c>GET /api/me</c>).</summary>
    Task<SessionDto> GetSessionAsync(CancellationToken cancellationToken = default);
}
