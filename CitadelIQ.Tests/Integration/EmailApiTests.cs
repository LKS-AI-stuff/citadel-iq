using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CitadelIQ.Application.Email;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

/// <summary>/api/email/* and /health/email through the real HTTP pipeline (sign-in, roles, 404s, CSRF).</summary>
[Collection(PostgresCollection.Name)]
public class EmailApiTests(PostgresFixture postgres)
{
    private static readonly Dictionary<string, string> AgentOn = new()
    {
        ["EmailAgent__Mode"] = "On",
        ["EmailAgent__SupportAddress"] = TestApp.SupportAddress,
        ["EmailAgent__PortalBaseUrl"] = TestApp.PortalBaseUrl
    };

    private static async Task<(TestUser Owner, TestUser Member, Guid DraftId)> OrganizationWithDraftAsync(TestApp app)
    {
        var (owner, _, _) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");
        app.Chat.CompleteHandler = m => m[0].Content.Contains("You classify one email")
            ? """{"kind":"question","question":"What about the weather?"}"""
            : "";
        app.Mailbox.Deliver(member.Email, "Weather?");
        await app.RunEmailAgentAsync(); // no documents ⇒ a draft
        var draftId = (await app.RunAsAsync(owner, sp => sp.GetRequiredService<IEmailReviewService>().ListDraftsAsync(EmailDraftStatus.Pending))).Single().Id;
        return (owner, member, draftId);
    }

    [Fact]
    public async Task Email_endpoints_require_sign_in_roles_an_organization_and_csrf()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, member, draftId) = await OrganizationWithDraftAsync(app);
        using var factory = ApiFactory.Create(app, AgentOn);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClientFor(null).GetAsync("/api/email/status")).StatusCode);

        var memberClient = factory.CreateClientFor(member);
        var status = await memberClient.GetFromJsonAsync<JsonElement>("/api/email/status");
        Assert.Equal(JsonValueKind.Null, status.GetProperty("supportAddress").ValueKind); // on, but no mailbox in this host
        Assert.Equal("On", status.GetProperty("mode").GetString());
        Assert.Equal(member.Email, status.GetProperty("yourAddress").GetString());
        Assert.True(status.GetProperty("canUse").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/email/drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/email/drafts/count")).StatusCode);
        var own = await memberClient.GetFromJsonAsync<JsonElement>("/api/email/activity");
        Assert.Equal("Drafted", Assert.Single(own.EnumerateArray()).GetProperty("outcome").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(app.DefaultUser).GetAsync("/api/email/drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(app.DefaultUser).GetAsync($"/api/email/drafts/{draftId}")).StatusCode);

        var ownerClient = factory.CreateClientFor(owner);
        Assert.Equal(1, (await ownerClient.GetFromJsonAsync<JsonElement>("/api/email/drafts/count")).GetProperty("pending").GetInt32());
        var draft = await ownerClient.GetFromJsonAsync<JsonElement>($"/api/email/drafts/{draftId}");
        Assert.Equal("NoAnswer", draft.GetProperty("reason").GetString());
        Assert.Equal("What about the weather?", draft.GetProperty("question").GetString());

        var noCsrf = factory.CreateClientFor(owner, csrfHeader: false);
        Assert.Equal(HttpStatusCode.BadRequest, (await noCsrf.PostAsJsonAsync($"/api/email/drafts/{draftId}/send", new { text = "Hi" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.PostAsJsonAsync($"/api/email/drafts/{draftId}/send", new { text = "Hi" })).StatusCode);
        var again = await ownerClient.PostAsJsonAsync($"/api/email/drafts/{draftId}/send", new { text = "Hi" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.PutAsJsonAsync("/api/email/notification-preference", new { emailOnNewDrafts = false })).StatusCode);
        Assert.False((await ownerClient.GetFromJsonAsync<JsonElement>("/api/email/notification-preference")).GetProperty("emailOnNewDrafts").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PutAsJsonAsync("/api/email/notification-preference", new { })).StatusCode);
    }

    [Fact]
    public async Task Status_hides_the_address_while_off_and_health_reports_the_mailbox()
    {
        await using var app = await postgres.CreateAppAsync();

        using (var off = ApiFactory.Create(app))
        {
            var status = await off.CreateClientFor(app.DefaultUser).GetFromJsonAsync<JsonElement>("/api/email/status");
            Assert.Equal(JsonValueKind.Null, status.GetProperty("supportAddress").ValueKind);
            Assert.Equal("Off", status.GetProperty("mode").GetString());
            Assert.Equal(HttpStatusCode.OK, (await off.CreateClientFor(null).GetAsync("/health/email")).StatusCode);
        }

        // On, but no mailbox is configured in this host: unhealthy.
        using var on = ApiFactory.Create(app, AgentOn);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await on.CreateClientFor(null).GetAsync("/health/email")).StatusCode);
    }
}
