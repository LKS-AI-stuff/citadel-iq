using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;
using CitadelIQ.Domain.Rules;

namespace CitadelIQ.Tests.Unit;

public class MembershipRulesTests
{
    private static Membership Member(WorkspaceRole role) => Membership.Create(Guid.NewGuid(), Guid.NewGuid(), role);

    [Fact]
    public void A_set_with_an_owner_passes()
    {
        MembershipRules.EnsureOwnerRemains([Member(WorkspaceRole.Owner), Member(WorkspaceRole.Member)]);
    }

    [Fact]
    public void A_set_without_an_owner_fails_with_a_safe_message()
    {
        var ex = Assert.Throws<DomainException>(() =>
            MembershipRules.EnsureOwnerRemains([Member(WorkspaceRole.Admin), Member(WorkspaceRole.Member)]));

        Assert.Equal("An organization must always have at least one Owner.", ex.Message);
    }

    [Fact]
    public void An_empty_set_fails()
    {
        Assert.Throws<DomainException>(() => MembershipRules.EnsureOwnerRemains([]));
    }

    [Theory]
    // Members manage nobody.
    [InlineData(WorkspaceRole.Member, WorkspaceRole.Member, WorkspaceRole.Admin, false)]
    [InlineData(WorkspaceRole.Member, WorkspaceRole.Member, null, false)]
    // Admins manage Members and Admins, but never touch Owners or create them.
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Admin, true)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Admin, WorkspaceRole.Member, true)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Member, null, true)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Admin, null, true)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Owner, false)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Owner, WorkspaceRole.Admin, false)]
    [InlineData(WorkspaceRole.Admin, WorkspaceRole.Owner, null, false)]
    // Owners manage everyone.
    [InlineData(WorkspaceRole.Owner, WorkspaceRole.Member, WorkspaceRole.Owner, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceRole.Owner, WorkspaceRole.Admin, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceRole.Owner, null, true)]
    public void CanManage_follows_the_permission_table(WorkspaceRole actor, WorkspaceRole target, WorkspaceRole? newRole, bool expected)
    {
        Assert.Equal(expected, MembershipRules.CanManage(actor, target, newRole));
    }
}
