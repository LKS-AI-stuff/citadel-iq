namespace CitadelIQ.Application.Options;

/// <summary>Sign-in and session settings (<c>Authentication</c> section).</summary>
public class AuthenticationOptions
{
    public const string OidcMode = "Oidc";
    public const string DevelopmentLoginMode = "DevelopmentLogin";

    /// <summary><c>Oidc</c> (Entra External ID or any OIDC provider) or <c>DevelopmentLogin</c> (a local sign-in form,
    /// allowed only in the Development environment).</summary>
    public string Mode { get; set; } = OidcMode;

    public OidcOptions Oidc { get; set; } = new();

    public SessionOptions Session { get; set; } = new();

    public JoinRateLimitOptions JoinRateLimit { get; set; } = new();
}

public class OidcOptions
{
    public string Authority { get; set; } = "";

    public string ClientId { get; set; } = "";

    /// <summary>Secret: user-secrets or <c>Authentication__Oidc__ClientSecret</c> only, never appsettings.json.</summary>
    public string ClientSecret { get; set; } = "";

    public List<string> Scopes { get; set; } = ["openid", "profile", "email"];
}

public class SessionOptions
{
    /// <summary>The <c>__Host-</c> prefix requires HTTPS (also in development — use the https launch profile).</summary>
    public string CookieName { get; set; } = "__Host-citadeliq";

    public double IdleTimeoutHours { get; set; } = 8;

    public double AbsoluteLifetimeDays { get; set; } = 7;
}

public class JoinRateLimitOptions
{
    public int PermitLimit { get; set; } = 10;

    public int WindowMinutes { get; set; } = 60;
}
