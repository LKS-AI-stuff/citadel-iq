using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CitadelIQ.Application.Organizations;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

/// <summary>The HTTP layer: authentication, onboarding gates, closed accounts, CSRF, roles, 404 across workspaces,
/// rate limiting and the startup guards — through the real middleware pipeline.</summary>
[Collection(PostgresCollection.Name)]
public class AuthApiTests(PostgresFixture postgres)
{
    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    [Fact]
    public async Task Signed_out_requests_get_401_except_public_endpoints()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);
        var anonymous = factory.CreateClientFor(null);

        var root = await anonymous.GetAsync("/api/folders/root");
        Assert.Equal(HttpStatusCode.Unauthorized, root.StatusCode); // (the body comes from the cookie handler in production)
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/search", new { query = "x", currentFolderId = app.Root, searchScope = "EntireWorkspace" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task With_the_real_cookie_and_oidc_handlers_api_calls_get_401_not_a_redirect_to_the_identity_provider()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app, useTestAuthentication: false);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var me = await client.GetAsync("/api/me");
        var root = await client.GetAsync("/api/folders/root");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("Please sign in.", await TitleAsync(me));
        Assert.Equal(HttpStatusCode.Unauthorized, root.StatusCode);
        Assert.Equal("application/problem+json", root.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Answer_rate_limits_are_per_user_not_per_ip()
    {
        await using var app = await postgres.CreateAppAsync();
        var other = await app.CreateUserAsync("Other");
        await app.OnboardIndividualAsync(other);
        using var factory = ApiFactory.Create(app, new() { ["Rag__RateLimit__PermitLimit"] = "1" });
        factory.Chat.StreamDeltas = ["ok"];

        HttpRequestMessage Ask(Guid root) => new(HttpMethod.Post, "/api/answers/stream")
        {
            Content = JsonContent.Create(new { question = "q", currentFolderId = root, searchScope = "EntireWorkspace", history = Array.Empty<object>() })
        };

        var first = factory.CreateClientFor(app.DefaultUser);
        Assert.Equal(HttpStatusCode.OK, (await first.SendAsync(Ask(app.Root))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.SendAsync(Ask(app.Root))).StatusCode);

        // Same IP (the test server), different user: its own budget.
        var otherRoot = (await app.SessionAsync(other)).Workspace!.RootFolderId;
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(other).SendAsync(Ask(otherRoot))).StatusCode);
    }

    [Fact]
    public async Task A_role_change_without_a_role_is_a_400_not_a_demotion()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", CitadelIQ.Domain.Enums.WorkspaceRole.Admin);
        using var factory = ApiFactory.Create(app);

        var response = await factory.CreateClientFor(owner).PutAsJsonAsync($"/api/organization/members/{admin.Id}/role", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CitadelIQ.Domain.Enums.WorkspaceRole.Admin, (await app.SessionAsync(admin)).Role);
    }

    [Fact]
    public async Task Onboarding_gates_content_until_the_user_has_a_workspace()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Newcomer");
        using var factory = ApiFactory.Create(app);
        var client = factory.CreateClientFor(user);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("NeedsOnboarding", me.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/folders/root")).StatusCode);

        var onboarded = await client.PostAsync("/api/onboarding/organization", JsonContent.Create(new { name = "Acme" }));
        Assert.Equal(HttpStatusCode.OK, onboarded.StatusCode);
        var session = await onboarded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", session.GetProperty("status").GetString());
        Assert.Equal("Owner", session.GetProperty("role").GetString());
        Assert.Equal("Organization", session.GetProperty("workspace").GetProperty("kind").GetString());

        var root = await client.GetFromJsonAsync<JsonElement>("/api/folders/root");
        Assert.Equal(session.GetProperty("workspace").GetProperty("rootFolderId").GetString(), root.GetProperty("folderId").GetString());
    }

    [Fact]
    public async Task A_first_request_from_an_unknown_identity_creates_the_user()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);
        var stranger = new TestUser(Guid.Empty, TestApp.TestIssuer, Guid.NewGuid().ToString("N"), "Stranger", "stranger@example.test");

        var me = await factory.CreateClientFor(stranger).GetFromJsonAsync<JsonElement>("/api/me");

        Assert.Equal("NeedsOnboarding", me.GetProperty("status").GetString());
        Assert.Equal("Stranger", me.GetProperty("user").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);
        var withoutHeader = factory.CreateClientFor(app.DefaultUser, csrfHeader: false);
        var withHeader = factory.CreateClientFor(app.DefaultUser);
        var body = new { parentFolderId = app.Root, name = "Reports" };

        var rejected = await withoutHeader.PostAsJsonAsync("/api/folders", body);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("The request is missing a required header.", await TitleAsync(rejected));
        Assert.Equal(HttpStatusCode.OK, (await withoutHeader.GetAsync($"/api/folders/{app.Root}/contents")).StatusCode); // GET is fine

        Assert.Equal(HttpStatusCode.OK, (await withHeader.PostAsJsonAsync("/api/folders", body)).StatusCode);
    }

    [Fact]
    public async Task Sign_out_needs_the_csrf_header_or_a_same_origin_post()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/logout", null)).StatusCode);

        var crossSite = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        crossSite.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(crossSite)).StatusCode);

        var sameOrigin = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        sameOrigin.Headers.Add("Origin", "http://localhost");
        var response = await client.SendAsync(sameOrigin);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Closed_accounts_are_rejected_everywhere_but_me()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");
        await app.RunAsAsync(owner, sp => sp.GetRequiredService<IOrganizationAdminService>().RemoveMemberAsync(member.Id));
        using var factory = ApiFactory.Create(app);
        var client = factory.CreateClientFor(member);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Closed", me.GetProperty("status").GetString());

        var root = await client.GetAsync("/api/folders/root");
        Assert.Equal(HttpStatusCode.Forbidden, root.StatusCode);
        Assert.Equal("Your account has been closed.", await TitleAsync(root));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/onboarding/individual", null)).StatusCode);
    }

    [Fact]
    public async Task Another_workspaces_document_is_404_over_http_too()
    {
        await using var app = await postgres.CreateAppAsync();
        var document = await app.UploadAndProcessAsync(app.Root, "secret.txt", TestFiles.Text("revenue"));
        var other = await app.CreateUserAsync("Other");
        await app.OnboardIndividualAsync(other);
        using var factory = ApiFactory.Create(app);
        var client = factory.CreateClientFor(other);

        var download = await client.GetAsync($"/api/documents/{document}/download");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal("Document not found.", await TitleAsync(download));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{document}/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/documents/{document}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/folders/{app.Root}/contents")).StatusCode);

        // The owner still downloads it.
        var own = await factory.CreateClientFor(app.DefaultUser).GetAsync($"/api/documents/{document}/download");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("revenue", await own.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Members_get_403_for_deletes_and_the_admin_api()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");
        var document = await app.UploadAsync(root, "a.txt", TestFiles.Text("x"), member);
        using var factory = ApiFactory.Create(app);
        var client = factory.CreateClientFor(member);

        var delete = await client.DeleteAsync($"/api/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal("You don't have permission to delete documents.", await TitleAsync(delete));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/organization/members")).StatusCode);

        var ownerClient = factory.CreateClientFor(owner);
        var members = await ownerClient.GetFromJsonAsync<JsonElement>("/api/organization/members");
        Assert.Equal(2, members.GetArrayLength());
        var promote = await ownerClient.PutAsJsonAsync($"/api/organization/members/{member.Id}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{document.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_last_owner_rule_is_a_400_over_http()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        using var factory = ApiFactory.Create(app);

        var response = await factory.CreateClientFor(owner).PostAsync("/api/organization/leave", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("An organization must always have at least one Owner.", await TitleAsync(response));
    }

    [Fact]
    public async Task Individual_workspaces_get_404_from_the_organization_api()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(app.DefaultUser).GetAsync("/api/organization/members")).StatusCode);
    }

    [Fact]
    public async Task Join_code_attempts_are_rate_limited_per_user()
    {
        await using var app = await postgres.CreateAppAsync();
        var user = await app.CreateUserAsync("Guesser");
        using var factory = ApiFactory.Create(app, new() { ["Authentication__JoinRateLimit__PermitLimit"] = "2" });
        var client = factory.CreateClientFor(user);

        Task<HttpResponseMessage> Guess() => client.PostAsJsonAsync("/api/onboarding/join-requests", new { joinCode = "ZZZZ-ZZZZ-ZZZZ" });

        Assert.Equal(HttpStatusCode.BadRequest, (await Guess()).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Guess()).StatusCode);
        var limited = await Guess();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("Too many attempts. Please wait a while and try again.", await TitleAsync(limited));
        // Another user has their own budget.
        var other = factory.CreateClientFor(await app.CreateUserAsync("Other"));
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/onboarding/join-requests", new { joinCode = "x" })).StatusCode);
    }

    [Fact]
    public async Task The_api_refuses_to_start_with_a_role_that_bypasses_row_level_security()
    {
        await using var app = await postgres.CreateAppAsync();

        var ex = Assert.ThrowsAny<Exception>(() => ApiFactory.Create(app, runtimeConnectionString: app.AdminConnectionString));

        Assert.Contains("BYPASSRLS", ex.ToString());
    }

    [Fact]
    public async Task Development_login_is_refused_outside_development()
    {
        await using var app = await postgres.CreateAppAsync();

        var ex = Assert.ThrowsAny<Exception>(() => ApiFactory.Create(app, new() { ["Authentication__Mode"] = "DevelopmentLogin" }));

        Assert.Contains("only allowed in the Development environment", ex.ToString());
    }

    [Fact]
    public async Task The_development_login_form_does_not_exist_in_oidc_mode()
    {
        await using var app = await postgres.CreateAppAsync();
        using var factory = ApiFactory.Create(app);

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(null).GetAsync("/auth/dev-login")).StatusCode);
    }

    [Fact]
    public async Task Join_flow_over_http_end_to_end()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var joiner = await app.CreateUserAsync("Joiner");
        using var factory = ApiFactory.Create(app);
        var ownerClient = factory.CreateClientFor(owner);
        var joinerClient = factory.CreateClientFor(joiner);

        var code = (await ownerClient.GetFromJsonAsync<JsonElement>("/api/organization/join-code")).GetProperty("code").GetString();
        var pending = await (await joinerClient.PostAsJsonAsync("/api/onboarding/join-requests", new { joinCode = code })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingApproval", pending.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await joinerClient.GetAsync("/api/folders/root")).StatusCode);

        var requests = await ownerClient.GetFromJsonAsync<JsonElement>("/api/organization/join-requests");
        var requestId = requests[0].GetProperty("id").GetString();
        var approved = await ownerClient.PostAsJsonAsync($"/api/organization/join-requests/{requestId}/approve", new { role = "Member" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        Assert.Equal("Active", (await joinerClient.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await joinerClient.GetAsync("/api/folders/root")).StatusCode);
    }
}
