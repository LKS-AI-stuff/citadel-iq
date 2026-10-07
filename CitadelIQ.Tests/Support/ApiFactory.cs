using System.Security.Claims;
using System.Text.Encodings.Web;
using CitadelIQ.Api.Authentication;
using CitadelIQ.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Tests.Support;

/// <summary>
/// The real ASP.NET pipeline (middleware, rate limiter, authorization, SSE) against a <see cref="TestApp"/> database,
/// with fake OpenAI services. Sign-in is replaced by <see cref="TestAuthHandler"/>: a request "is" a user when it
/// carries <c>X-Test-User</c> (see <see cref="CreateClientFor"/>); the rest of the identity pipeline — the
/// current-user middleware, closed accounts, the fallback policy — is the production code.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>False keeps the production cookie/OIDC handlers (anonymous requests only).</summary>
    private bool _useTestAuthentication = true;

    public FakeChatCompletionService Chat { get; } = new();
    public FakeEmbeddingService Embeddings { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // don't pick up the developer's user-secrets
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IChatCompletionService>(Chat);
            services.AddSingleton<IOpenAIEmbeddingService>(Embeddings);
            if (!_useTestAuthentication)
            {
                return;
            }

            services.AddAuthentication(o =>
                {
                    o.DefaultScheme = TestAuthHandler.Scheme;
                    o.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    o.DefaultForbidScheme = TestAuthHandler.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });
    }

    /// <summary>Program.cs reads configuration eagerly, so settings are passed as environment variables that
    /// exist only while the host is built.</summary>
    public static ApiFactory Create(TestApp app, Dictionary<string, string>? settings = null, string? runtimeConnectionString = null, bool useTestAuthentication = true)
    {
        var env = new Dictionary<string, string>
        {
            ["ConnectionStrings__CitadelIQ"] = runtimeConnectionString ?? app.AppConnectionString,
            // Same file store as the TestApp (an absolute path wins over the host's content root).
            ["Storage__DocumentsPath"] = app.DocumentsDirectory,
            // Placeholders: OIDC is configured (as in production) but never used — TestAuthHandler signs requests in.
            ["Authentication__Oidc__Authority"] = "https://login.example.test/",
            ["Authentication__Oidc__ClientId"] = "test-client",
            ["Authentication__Oidc__ClientSecret"] = "test-secret"
        };
        foreach (var (k, v) in settings ?? []) env[k] = v;

        foreach (var (k, v) in env) Environment.SetEnvironmentVariable(k, v);
        try
        {
            var factory = new ApiFactory { _useTestAuthentication = useTestAuthentication };
            _ = factory.Server; // build the host while the variables are set
            return factory;
        }
        finally
        {
            foreach (var k in env.Keys) Environment.SetEnvironmentVariable(k, null);
        }
    }

    /// <summary>A client signed in as <paramref name="user"/> that sends the CSRF header like the SPA does.</summary>
    public HttpClient CreateClientFor(TestUser? user, bool csrfHeader = true)
    {
        var client = CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.Header, TestAuthHandler.Encode(user));
        }

        if (csrfHeader)
        {
            client.DefaultRequestHeaders.Add(CsrfHeaderMiddleware.HeaderName, CsrfHeaderMiddleware.HeaderValue);
        }

        return client;
    }
}

/// <summary>Signs a request in from the <c>X-Test-User</c> header (issuer|subject|name|email); anonymous otherwise.</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Test";
    public const string Header = "X-Test-User";

    public static string Encode(TestUser user) => string.Join('|', user.Issuer, user.Subject, user.DisplayName, user.Email);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = value.Split('|');
        ClaimsPrincipal principal = SessionClaims.Create(parts[0], parts[1], parts[2], parts[3]);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
