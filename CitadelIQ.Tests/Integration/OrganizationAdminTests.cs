using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Organizations;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;
using CitadelIQ.Domain.Rules;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class OrganizationAdminTests(PostgresFixture postgres)
{
    private static Task<T> Admin<T>(TestApp app, TestUser actor, Func<IOrganizationAdminService, Task<T>> action) =>
        app.RunAsAsync(actor, sp => action(sp.GetRequiredService<IOrganizationAdminService>()));

    private static Task Admin(TestApp app, TestUser actor, Func<IOrganizationAdminService, Task> action) =>
        app.RunAsAsync(actor, sp => action(sp.GetRequiredService<IOrganizationAdminService>()));

    private static async Task<WorkspaceRole> RoleOf(TestApp app, TestUser user) => (await app.SessionAsync(user)).Role!.Value;

    [Fact]
    public async Task Owners_and_admins_see_the_member_list_members_do_not()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme", "Olivia Owner");
        var admin = await app.AddMemberAsync(owner, "Adam Admin", WorkspaceRole.Admin);
        var member = await app.AddMemberAsync(owner, "Mia Member");

        var members = await Admin(app, admin, s => s.ListMembersAsync());

        Assert.Equal(["Adam Admin", "Mia Member", "Olivia Owner"], members.Select(m => m.DisplayName));
        Assert.Equal([WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Owner], members.Select(m => m.Role));
        Assert.True(members.Single(m => m.UserId == admin.Id).IsCurrentUser);
        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, member, s => s.ListMembersAsync()));
        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, member, s => s.GetJoinCodeAsync()));
    }

    [Fact]
    public async Task Individual_workspaces_have_no_organization_administration()
    {
        await using var app = await postgres.CreateAppAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => Admin(app, app.DefaultUser, s => s.ListMembersAsync()));
        await Assert.ThrowsAsync<NotFoundException>(() => Admin(app, app.DefaultUser, s => s.GetJoinCodeAsync()));
        await Assert.ThrowsAsync<NotFoundException>(() => Admin(app, app.DefaultUser, s => s.LeaveAsync()));
    }

    [Fact]
    public async Task Admins_manage_members_and_admins_but_not_owners()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);
        var member = await app.AddMemberAsync(owner, "Member");

        var promoted = await Admin(app, admin, s => s.ChangeRoleAsync(member.Id, WorkspaceRole.Admin));
        Assert.Equal(WorkspaceRole.Admin, promoted.Role);
        await Admin(app, admin, s => s.ChangeRoleAsync(member.Id, WorkspaceRole.Member));

        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, admin, s => s.ChangeRoleAsync(member.Id, WorkspaceRole.Owner)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, admin, s => s.ChangeRoleAsync(owner.Id, WorkspaceRole.Member)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, admin, s => s.RemoveMemberAsync(owner.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(app, member, s => s.ChangeRoleAsync(admin.Id, WorkspaceRole.Member)));
        Assert.Equal(WorkspaceRole.Owner, await RoleOf(app, owner));
    }

    [Fact]
    public async Task Owners_can_create_owners_and_step_down_while_another_owner_remains()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var second = await app.AddMemberAsync(owner, "Second");

        await Admin(app, owner, s => s.ChangeRoleAsync(second.Id, WorkspaceRole.Owner));
        await Admin(app, owner, s => s.ChangeRoleAsync(owner.Id, WorkspaceRole.Member));

        Assert.Equal(WorkspaceRole.Owner, await RoleOf(app, second));
        Assert.Equal(WorkspaceRole.Member, await RoleOf(app, owner));
    }

    [Fact]
    public async Task The_last_owner_cannot_demote_remove_or_leave()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);

        var demote = await Assert.ThrowsAsync<DomainException>(() => Admin(app, owner, s => s.ChangeRoleAsync(owner.Id, WorkspaceRole.Admin)));
        Assert.Equal(MembershipRules.LastOwnerMessage, demote.Message);
        await Assert.ThrowsAsync<DomainException>(() => Admin(app, owner, s => s.RemoveMemberAsync(owner.Id)));
        await Assert.ThrowsAsync<DomainException>(() => Admin(app, owner, s => s.LeaveAsync()));

        // Nothing changed: still an active owner.
        var session = await app.SessionAsync(owner);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(WorkspaceRole.Owner, session.Role);
    }

    [Fact]
    public async Task Removing_a_member_closes_their_account_and_keeps_their_documents()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Leaving Member");
        var document = await app.UploadAndProcessAsync(root, "kept.txt", TestFiles.Text("revenue"), member);

        await Admin(app, owner, s => s.RemoveMemberAsync(member.Id));

        Assert.Equal(SessionStatus.Closed, (await app.SessionAsync(member)).Status);
        Assert.DoesNotContain(await Admin(app, owner, s => s.ListMembersAsync()), m => m.UserId == member.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => app.GetStatusAsync(document, member));
        Assert.Equal(ProcessingStatus.Ready, (await app.GetStatusAsync(document, owner)).Status);
        // A closed account can never onboard again.
        await Assert.ThrowsAsync<ForbiddenException>(() => app.OnboardIndividualAsync(member));
    }

    [Fact]
    public async Task A_member_can_leave_and_their_account_is_closed()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");

        await Admin(app, member, s => s.LeaveAsync());

        Assert.Equal(SessionStatus.Closed, (await app.SessionAsync(member)).Status);
        Assert.Single(await Admin(app, owner, s => s.ListMembersAsync()));
    }

    [Fact]
    public async Task Only_owners_approve_someone_as_owner()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);
        var joiner = await app.CreateUserAsync("Joiner");
        await app.RequestToJoinAsync(joiner, await app.GetJoinCodeAsync(admin));
        var request = Assert.Single(await app.ListJoinRequestsAsync(admin));

        await Assert.ThrowsAsync<ForbiddenException>(() => app.ApproveAsync(admin, request.Id, WorkspaceRole.Owner));
        var approved = await app.ApproveAsync(owner, request.Id, WorkspaceRole.Owner);

        Assert.Equal(WorkspaceRole.Owner, approved.Role);
        await Assert.ThrowsAsync<NotFoundException>(() => app.ApproveAsync(owner, request.Id)); // already decided
    }

    [Fact]
    public async Task Join_requests_of_other_organizations_are_not_found()
    {
        await using var app = await postgres.CreateAppAsync();
        var (acmeOwner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme", "Acme Owner");
        var (otherOwner, _, _) = await app.CreateOrganizationWithOwnerAsync("Other", "Other Owner");
        var joiner = await app.CreateUserAsync("Joiner");
        await app.RequestToJoinAsync(joiner, await app.GetJoinCodeAsync(acmeOwner));
        var request = Assert.Single(await app.ListJoinRequestsAsync(acmeOwner));

        Assert.Empty(await app.ListJoinRequestsAsync(otherOwner));
        await Assert.ThrowsAsync<NotFoundException>(() => app.ApproveAsync(otherOwner, request.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Admin(app, otherOwner, s => s.RemoveMemberAsync(acmeOwner.Id)));
    }

    [Fact]
    public async Task Regenerating_the_join_code_invalidates_the_old_one()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var oldCode = await app.GetJoinCodeAsync(owner);

        var newCode = (await Admin(app, owner, s => s.RegenerateJoinCodeAsync())).Code;

        Assert.NotEqual(oldCode, newCode);
        Assert.Equal(newCode, await app.GetJoinCodeAsync(owner));
        var late = await app.CreateUserAsync("Late");
        await Assert.ThrowsAsync<ValidationException>(() => app.RequestToJoinAsync(late, oldCode));
        Assert.Equal(SessionStatus.PendingApproval, (await app.RequestToJoinAsync(await app.CreateUserAsync("On time"), newCode)).Status);
    }

    [Fact]
    public async Task Two_owners_demoting_each_other_at_once_always_leave_one_owner()
    {
        await using var app = await postgres.CreateAppAsync();

        for (var i = 0; i < 20; i++)
        {
            var (a, _, _) = await app.CreateOrganizationWithOwnerAsync($"Race {i}", $"A{i}");
            var b = await app.AddMemberAsync(a, $"B{i}", WorkspaceRole.Member);
            await Admin(app, a, s => s.ChangeRoleAsync(b.Id, WorkspaceRole.Owner));

            async Task<bool> Demote(TestUser actor, TestUser target)
            {
                try
                {
                    await Admin(app, actor, s => s.ChangeRoleAsync(target.Id, WorkspaceRole.Admin));
                    return true;
                }
                catch (Exception ex) when (ex is ForbiddenException or DomainException)
                {
                    return false;
                }
            }

            var outcomes = await Task.WhenAll(Demote(a, b), Demote(b, a));

            Assert.Equal(1, outcomes.Count(o => o));
            var members = await Admin(app, (await RoleOf(app, a)) == WorkspaceRole.Owner ? a : b, s => s.ListMembersAsync());
            Assert.Single(members, m => m.Role == WorkspaceRole.Owner);
        }
    }

    [Fact]
    public async Task A_sign_in_racing_a_removal_does_not_reopen_the_account()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");

        await app.RunAsAsync(null, async sp =>
        {
            // The sign-in loads the (still open) account …
            var users = sp.GetRequiredService<CitadelIQ.Application.Interfaces.IUserAccountRepository>();
            var loaded = await users.GetByIdentityAsync(member.Issuer, member.Subject);

            // … an admin removes the member in another request …
            await Admin(app, owner, s => s.RemoveMemberAsync(member.Id));

            // … and the sign-in then saves its profile refresh.
            loaded!.RecordSignIn("member@example.test", "Renamed");
            await users.UpdateAsync(loaded);
        });

        Assert.Equal(SessionStatus.Closed, (await app.SessionAsync(member)).Status);
    }

    [Fact]
    public async Task A_demoted_admin_cannot_reject_requests()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);
        var joiner = await app.CreateUserAsync("Joiner");
        await app.RequestToJoinAsync(joiner, await app.GetJoinCodeAsync(owner));
        var request = Assert.Single(await app.ListJoinRequestsAsync(owner));

        await app.RunAsAsync(admin, async sp =>
        {
            // Demoted after this request resolved its role, but before it took the lock.
            await Admin(app, owner, s => s.ChangeRoleAsync(admin.Id, WorkspaceRole.Member));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                sp.GetRequiredService<IOrganizationAdminService>().RejectJoinRequestAsync(request.Id));
        });

        Assert.Single(await app.ListJoinRequestsAsync(owner));
    }

    [Fact]
    public async Task Two_admins_approving_the_same_request_at_once_add_one_membership()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);
        var joiner = await app.CreateUserAsync("Joiner");
        await app.RequestToJoinAsync(joiner, await app.GetJoinCodeAsync(owner));
        var request = Assert.Single(await app.ListJoinRequestsAsync(owner));

        async Task<bool> Approve(TestUser actor)
        {
            try
            {
                await app.ApproveAsync(actor, request.Id);
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(Approve(owner), Approve(admin));

        Assert.Equal(1, outcomes.Count(o => o));
        Assert.Equal(3, (await Admin(app, owner, s => s.ListMembersAsync())).Count);
    }
}
