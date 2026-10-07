using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class OnboardingTests(PostgresFixture postgres)
{
    private static Task<SessionDto> Cancel(TestApp app, TestUser user) =>
        app.RunAsAsync(user, sp => sp.GetRequiredService<IOnboardingService>().CancelJoinRequestAsync());

    [Fact]
    public async Task A_new_user_needs_onboarding()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Newcomer");

        var session = await app.SessionAsync(user);

        Assert.Equal(SessionStatus.NeedsOnboarding, session.Status);
        Assert.Equal("Newcomer", session.User.DisplayName);
        Assert.Null(session.Workspace);
        Assert.Null(session.Role);
    }

    [Fact]
    public async Task Signing_in_again_refreshes_the_profile_but_keeps_the_account()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Old Name");

        var again = await app.RunAsAsync(null, sp => sp.GetRequiredService<IAccountService>()
            .EnsureUserAsync(user.Issuer, user.Subject, "new@example.test", "New Name"));

        Assert.Equal(user.Id, again.Id);
        Assert.Equal("New Name", again.DisplayName);
        Assert.Equal("new@example.test", again.Email);
        Assert.Equal(1, await app.ScalarAsync<long>($"SELECT count(*) FROM \"Users\" WHERE \"Subject\" = '{user.Subject}'"));
    }

    [Fact]
    public async Task Concurrent_first_sign_ins_create_one_user()
    {
        await using var app = await postgres.CreateAppAsync();
        var subject = Guid.NewGuid().ToString("N");

        var users = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            app.RunAsAsync(null, sp => sp.GetRequiredService<IAccountService>().EnsureUserAsync(TestApp.TestIssuer, subject, "x@example.test", "X"))));

        Assert.Single(users.Select(u => u.Id).Distinct());
    }

    [Fact]
    public async Task Individual_onboarding_makes_the_user_owner_of_a_private_workspace()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Jane Doe");

        var session = await app.OnboardIndividualAsync(user);

        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(WorkspaceRole.Owner, session.Role);
        Assert.Equal(WorkspaceKind.Individual, session.Workspace!.Kind);
        Assert.Equal("Jane Doe", session.Workspace.Name);
        Assert.NotEqual(app.Root, session.Workspace.RootFolderId);
        Assert.Equal(0, await app.ScalarAsync<long>($"SELECT count(*) FROM \"Workspaces\" WHERE \"Id\" = '{session.Workspace.Id}' AND \"JoinCode\" IS NOT NULL"));
    }

    [Fact]
    public async Task Creating_an_organization_makes_the_creator_its_owner_with_a_join_code()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");

        var session = await app.SessionAsync(owner);
        var code = await app.GetJoinCodeAsync(owner);

        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(WorkspaceRole.Owner, session.Role);
        Assert.Equal(WorkspaceKind.Organization, session.Workspace!.Kind);
        Assert.Equal("Acme", session.Workspace.Name);
        Assert.Matches("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", code);
    }

    [Fact]
    public async Task A_user_with_a_workspace_cannot_onboard_again()
    {
        await using var app = await postgres.CreateAppAsync();

        await Assert.ThrowsAsync<ValidationException>(() => app.OnboardIndividualAsync(app.DefaultUser));
        await Assert.ThrowsAsync<ValidationException>(() => app.CreateOrganizationAsync(app.DefaultUser, "Second"));
    }

    [Fact]
    public async Task Concurrent_onboarding_creates_exactly_one_workspace()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Racer");

        async Task<bool> Try(Func<Task> action)
        {
            try
            {
                await action();
                return true;
            }
            catch (ValidationException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(
            Try(() => app.OnboardIndividualAsync(user)),
            Try(() => app.CreateOrganizationAsync(user, "Org")),
            Try(() => app.OnboardIndividualAsync(user)));

        Assert.Equal(1, outcomes.Count(o => o));
        Assert.Equal(1, await app.ScalarAsync<long>($"SELECT count(*) FROM \"Memberships\" WHERE \"UserId\" = '{user.Id}'"));
    }

    [Fact]
    public async Task Organization_names_are_validated()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Someone");

        await Assert.ThrowsAsync<CitadelIQ.Domain.Exceptions.DomainException>(() => app.CreateOrganizationAsync(user, " "));
        Assert.Equal(SessionStatus.NeedsOnboarding, (await app.SessionAsync(user)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-code")]
    [InlineData("ZZZZ-ZZZZ-ZZZZ")] // well-formed but unknown
    public async Task Unknown_join_codes_get_one_generic_message(string code)
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Joiner");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => app.RequestToJoinAsync(user, code));

        Assert.Equal(OnboardingService.UnrecognisedCodeMessage, ex.Message);
    }

    [Fact]
    public async Task Joining_is_pending_until_an_admin_approves()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, orgId, orgRoot) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var joiner = await app.CreateUserAsync("Joiner");

        var code = (await app.GetJoinCodeAsync(owner)).ToLowerInvariant().Replace("-", ""); // codes are forgiving
        var pending = await app.RequestToJoinAsync(joiner, code);

        Assert.Equal(SessionStatus.PendingApproval, pending.Status);
        Assert.Equal("Acme", pending.PendingJoinRequest!.OrganizationName);
        await Assert.ThrowsAsync<NotFoundException>(() => app.RunAsAsync(joiner,
            sp => sp.GetRequiredService<CitadelIQ.Application.Folders.IFolderService>().GetContentsAsync(orgRoot)));

        var request = Assert.Single(await app.ListJoinRequestsAsync(owner));
        Assert.Equal(joiner.Email, request.Email);
        await app.ApproveAsync(owner, request.Id, WorkspaceRole.Admin);

        var active = await app.SessionAsync(joiner);
        Assert.Equal(SessionStatus.Active, active.Status);
        Assert.Equal(orgId, active.Workspace!.Id);
        Assert.Equal(orgRoot, active.Workspace.RootFolderId);
        Assert.Equal(WorkspaceRole.Admin, active.Role);
        Assert.Empty(await app.ListJoinRequestsAsync(owner));
    }

    [Fact]
    public async Task A_pending_user_cannot_start_another_onboarding_until_they_cancel()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var joiner = await app.CreateUserAsync("Joiner");
        var code = await app.GetJoinCodeAsync(owner);
        await app.RequestToJoinAsync(joiner, code);

        await Assert.ThrowsAsync<ValidationException>(() => app.OnboardIndividualAsync(joiner));
        await Assert.ThrowsAsync<ValidationException>(() => app.RequestToJoinAsync(joiner, code));

        var cancelled = await Cancel(app, joiner);

        Assert.Equal(SessionStatus.NeedsOnboarding, cancelled.Status);
        Assert.Null(cancelled.LastRejectedOrganizationName);
        Assert.Empty(await app.ListJoinRequestsAsync(owner));
        Assert.Equal(SessionStatus.Active, (await app.OnboardIndividualAsync(joiner)).Status);
    }

    [Fact]
    public async Task Cancelling_without_a_pending_request_is_not_found()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Nobody");

        await Assert.ThrowsAsync<NotFoundException>(() => Cancel(app, user));
    }

    [Fact]
    public async Task A_rejected_user_is_told_and_can_onboard_again()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var joiner = await app.CreateUserAsync("Joiner");
        await app.RequestToJoinAsync(joiner, await app.GetJoinCodeAsync(owner));
        var request = Assert.Single(await app.ListJoinRequestsAsync(owner));

        await app.RunAsAsync(owner, sp => sp.GetRequiredService<CitadelIQ.Application.Organizations.IOrganizationAdminService>().RejectJoinRequestAsync(request.Id));

        var session = await app.SessionAsync(joiner);
        Assert.Equal(SessionStatus.NeedsOnboarding, session.Status);
        Assert.Equal("Acme", session.LastRejectedOrganizationName);
        Assert.Equal(SessionStatus.Active, (await app.OnboardIndividualAsync(joiner)).Status);
    }
}
