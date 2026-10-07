using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Accounts;

/// <summary>
/// The signed-in user for the current scope (an HTTP request), resolved from the database on every request so that
/// role changes and removals apply immediately. Empty for anonymous requests and background work.
/// </summary>
public interface ICurrentUser
{
    UserAccount? User { get; }
    Membership? Membership { get; }
    Workspace? Workspace { get; }

    Guid? UserId => User?.Id;
    Guid? WorkspaceId => Workspace?.Id;
    WorkspaceRole? Role => Membership?.Role;

    /// <summary>Has a membership and the account is not closed.</summary>
    bool IsActiveMember => User is { IsClosed: false } && Membership is not null && Workspace is not null;

    /// <summary>Returns the user id or throws <see cref="Common.UnauthenticatedException"/>.</summary>
    Guid RequireUserId();

    /// <summary>Throws <see cref="Common.ForbiddenException"/> ("You don't have permission to {action}.") unless the
    /// caller is an active member with at least <paramref name="minimum"/>.</summary>
    void EnsureRole(WorkspaceRole minimum, string action);
}
