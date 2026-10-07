using CitadelIQ.Api.Authentication;
using CitadelIQ.Application.Options;

namespace CitadelIQ.Tests.Unit;

public class AuthenticationOptionsTests
{
    private static readonly AuthenticationOptionsValidator Validator = new();

    [Fact]
    public void Oidc_mode_requires_authority_client_id_and_secret()
    {
        var result = Validator.Validate(null, new AuthenticationOptions { Mode = AuthenticationOptions.OidcMode });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Authority"));
        Assert.Contains(result.Failures!, f => f.Contains("ClientId"));
        Assert.Contains(result.Failures!, f => f.Contains("ClientSecret"));
    }

    [Fact]
    public void A_complete_oidc_configuration_is_valid()
    {
        var options = new AuthenticationOptions
        {
            Oidc = new OidcOptions { Authority = "https://login.example.test/", ClientId = "id", ClientSecret = "secret" }
        };

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void Development_login_needs_no_oidc_settings()
    {
        Assert.True(Validator.Validate(null, new AuthenticationOptions { Mode = AuthenticationOptions.DevelopmentLoginMode }).Succeeded);
    }

    [Fact]
    public void Unknown_modes_and_non_positive_limits_are_rejected()
    {
        var options = new AuthenticationOptions
        {
            Mode = "Basic",
            Session = new SessionOptions { CookieName = "", IdleTimeoutHours = 0, AbsoluteLifetimeDays = -1 },
            JoinRateLimit = new JoinRateLimitOptions { PermitLimit = 0, WindowMinutes = 0 }
        };

        var result = Validator.Validate(null, options);

        Assert.Equal(6, result.Failures!.Count());
    }

    [Fact]
    public void Session_claims_round_trip_the_identity_and_sign_in_time()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var principal = SessionClaims.Create("https://issuer", "subject-1", "Jane", "jane@example.test");

        Assert.True(SessionClaims.TryGetIdentity(principal, out var issuer, out var subject));
        Assert.Equal("https://issuer", issuer);
        Assert.Equal("subject-1", subject);
        Assert.Equal("Jane", principal.Identity!.Name);
        Assert.True(principal.Identity.IsAuthenticated);
        Assert.InRange(SessionClaims.GetSignedInAt(principal)!.Value, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
