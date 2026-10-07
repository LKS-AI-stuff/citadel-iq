using CitadelIQ.Application.Common;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Accounts;

/// <summary>
/// Scoped holder behind both <see cref="ICurrentUser"/> and <see cref="IWorkspaceContext"/>. Filled by the Api's
/// current-user middleware per request, by the processing dispatcher for background work, and by onboarding when a
/// workspace is created. Contains no ASP.NET types.
/// </summary>
public class CurrentUserContext : ICurrentUser, IWorkspaceContext
{
    private Guid? _workspaceId;

    public UserAccount? User { get; private set; }
    public Membership? Membership { get; private set; }
    public Workspace? Workspace { get; private set; }

    Guid? IWorkspaceContext.WorkspaceId => _workspaceId;

    public void SetUser(UserAccount user, Membership? membership, Workspace? workspace)
    {
        User = user;
        Membership = membership;
        Workspace = workspace;

        // A closed account never gets a workspace context, so even a bug further down cannot read content for it.
        if (workspace is not null && membership is not null && !user.IsClosed)
        {
            Enter(workspace.Id);
        }
    }

    public void Enter(Guid workspaceId)
    {
        if (_workspaceId is { } current && current != workspaceId)
        {
            throw new InvalidOperationException("This scope is already bound to a different workspace.");
        }

        _workspaceId = workspaceId;
    }

    public Guid RequireUserId() => User?.Id ?? throw new UnauthenticatedException();

    public void EnsureRole(WorkspaceRole minimum, string action)
    {
        if (!((ICurrentUser)this).IsActiveMember || Membership!.Role < minimum)
        {
            throw new ForbiddenException($"You don't have permission to {action}.");
        }
    }
}
