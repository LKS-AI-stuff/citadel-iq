using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Rules;

namespace CitadelIQ.Tests.Unit;

public class CurrentUserContextTests
{
    private static (UserAccount User, Workspace Workspace, Membership Membership) Member(WorkspaceRole role)
    {
        var user = UserAccount.Create("iss", Guid.NewGuid().ToString(), "a@example.test", "A");
        var workspace = Workspace.CreateOrganization("Acme", JoinCode.Generate());
        return (user, workspace, Membership.Create(user.Id, workspace.Id, role));
    }

    [Fact]
    public void A_scope_cannot_switch_workspaces()
    {
        var context = new CurrentUserContext();
        var workspaceId = Guid.NewGuid();

        context.Enter(workspaceId);
        context.Enter(workspaceId); // same one: fine

        Assert.Equal(workspaceId, ((IWorkspaceContext)context).WorkspaceId);
        Assert.Throws<InvalidOperationException>(() => context.Enter(Guid.NewGuid()));
    }

    [Fact]
    public void Setting_an_active_member_enters_their_workspace()
    {
        var (user, workspace, membership) = Member(WorkspaceRole.Member);
        var context = new CurrentUserContext();

        context.SetUser(user, membership, workspace);

        ICurrentUser current = context;
        Assert.True(current.IsActiveMember);
        Assert.Equal(workspace.Id, ((IWorkspaceContext)context).WorkspaceId);
        Assert.Equal(WorkspaceRole.Member, current.Role);
    }

    [Fact]
    public void A_closed_account_never_gets_a_workspace()
    {
        var (user, workspace, membership) = Member(WorkspaceRole.Owner);
        user.Close();
        var context = new CurrentUserContext();

        context.SetUser(user, membership, workspace);

        Assert.False(((ICurrentUser)context).IsActiveMember);
        Assert.Null(((IWorkspaceContext)context).WorkspaceId);
        Assert.Throws<ForbiddenException>(() => context.EnsureRole(WorkspaceRole.Member, "read"));
    }

    [Fact]
    public void EnsureRole_compares_against_the_minimum()
    {
        var (user, workspace, membership) = Member(WorkspaceRole.Admin);
        var context = new CurrentUserContext();
        context.SetUser(user, membership, workspace);

        context.EnsureRole(WorkspaceRole.Member, "read");
        context.EnsureRole(WorkspaceRole.Admin, "delete documents");
        var ex = Assert.Throws<ForbiddenException>(() => context.EnsureRole(WorkspaceRole.Owner, "manage owners"));
        Assert.Equal("You don't have permission to manage owners.", ex.Message);
    }

    [Fact]
    public void Anonymous_scopes_have_no_user()
    {
        var context = new CurrentUserContext();

        Assert.Throws<UnauthenticatedException>(() => context.RequireUserId());
        Assert.Throws<ForbiddenException>(() => context.EnsureRole(WorkspaceRole.Member, "read"));
    }
}
