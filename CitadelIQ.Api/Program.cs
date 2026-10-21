using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CitadelIQ.Api.Authentication;
using CitadelIQ.Api.Controllers;
using CitadelIQ.Api.Middleware;
using CitadelIQ.Application;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.FluentMigrations;
using CitadelIQ.Infrastructure;
using CitadelIQ.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using AppAuthenticationOptions = CitadelIQ.Application.Options.AuthenticationOptions;

var builder = WebApplication.CreateBuilder(args);

// Same origin by default (the UI reaches the API through the Vite proxy / nginx), so no CORS origins are needed.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection("Chunking"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddOptions<StorageOptions>().Bind(builder.Configuration.GetSection("Storage")).ValidateOnStart();
builder.Services.AddOptions<RagOptions>().Bind(builder.Configuration.GetSection("Rag")).ValidateOnStart();
builder.Services.AddOptions<AppAuthenticationOptions>().Bind(builder.Configuration.GetSection("Authentication")).ValidateOnStart();
builder.Services.AddOptions<EmailAgentOptions>().Bind(builder.Configuration.GetSection("EmailAgent")).ValidateOnStart();

builder.AddCitadelDataProtection();
builder.AddCitadelAuthentication();
var joinRateLimit = builder.Configuration.GetSection("Authentication:JoinRateLimit").Get<JoinRateLimitOptions>() ?? new JoinRateLimitOptions();

// Per-user limits (per IP when signed out) on /api/answers only: a request window plus a concurrent-stream cap (an SSE
// response holds its permit until the stream ends), and a named policy for join-code attempts. Everything else is
// unlimited. The partition key is read after CurrentUserMiddleware has resolved the user.
static string ClientKey(HttpContext context) =>
    context.RequestServices.GetService<ICurrentUser>()?.UserId is { } userId
        ? $"user:{userId}"
        : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

var rateLimit = builder.Configuration.GetSection("Rag:RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/api/answers")
                ? RateLimitPartition.GetConcurrencyLimiter(
                    ClientKey(context),
                    _ => new ConcurrencyLimiterOptions { PermitLimit = rateLimit.MaxConcurrentStreams, QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("other")),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/api/answers")
                ? RateLimitPartition.GetSlidingWindowLimiter(
                    ClientKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimit.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimit.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0
                    })
                : RateLimitPartition.GetNoLimiter("other")));
    options.AddPolicy(OnboardingController.JoinRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = joinRateLimit.PermitLimit,
                Window = TimeSpan.FromMinutes(joinRateLimit.WindowMinutes),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = context.HttpContext.Request.Path.StartsWithSegments("/api/onboarding")
                ? "Too many attempts. Please wait a while and try again."
                : "You're asking questions too quickly. Please wait a moment and try again."
        }, options: null, contentType: "application/problem+json", cancellationToken);
    };
});

// Off by default. Behind a reverse proxy, list its address(es) so the rate limiter sees the real client IP.
var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
if (knownProxies.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        // Host too: the OIDC redirect_uri is built from the public host nginx forwards.
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();
        foreach (var proxy in knownProxies)
        {
            o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
        }
    });
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var connectionString = builder.Configuration.GetConnectionString("CitadelIQ");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:CitadelIQ' is not configured. " +
        "Set it via dotnet user-secrets or the ConnectionStrings__CitadelIQ environment variable.");
}

// Migrations run as the database owner; the API itself runs as the restricted runtime role (checked below).
var migrateOnStartup = builder.Configuration.GetValue<bool>("Database:MigrateOnStartup");
var migrationsConnectionString = builder.Configuration.GetConnectionString("CitadelIQMigrations");
if (migrateOnStartup && string.IsNullOrWhiteSpace(migrationsConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:CitadelIQMigrations' (the owner role) is required when Database:MigrateOnStartup is true. " +
        "Set it via dotnet user-secrets or the ConnectionStrings__CitadelIQMigrations environment variable.");
}

builder.Services.AddFluentMigrations(string.IsNullOrWhiteSpace(migrationsConnectionString) ? connectionString : migrationsConnectionString);

var app = builder.Build();

// First in the pipeline so redirection, rate limiting and logging see the real client address/scheme.
if (knownProxies.Length > 0)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Enabled in appsettings.Development.json; in deployed environments set Database__MigrateOnStartup=true
// (e.g. in docker-compose) or run the migrations as a separate step.
if (migrateOnStartup)
{
    app.Services.ApplyDatabaseMigrations();
}

// On without a mailbox is allowed (local development and tests run that way) but does nothing: say so loudly.
if (app.Services.GetRequiredService<IOptionsMonitor<EmailAgentOptions>>().CurrentValue.Mode != EmailAgentMode.Off
    && !app.Services.GetRequiredService<IEmailAgentMonitor>().MailboxConfigured)
{
    app.Logger.LogWarning("EmailAgent:Mode is not Off but no support mailbox is configured (EmailAgent:Mailbox:Host); the email agent will not run");
}

// Refuse to start if the runtime role would bypass row-level security (superuser / BYPASSRLS).
await app.Services.EnsureRuntimeRoleEnforcesRowLevelSecurityAsync();

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseMiddleware<CurrentUserMiddleware>();

app.UseMiddleware<CsrfHeaderMiddleware>();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

// Reachability of the document store; only remote providers (Azure Blob) implement the probe.
app.MapGet("/health/storage", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    var probe = services.GetService<CitadelIQ.Application.Interfaces.IStorageProbe>();
    if (probe is null)
    {
        return Results.Ok(new { status = "healthy", provider = StorageOptions.LocalDiskProvider });
    }

    return await probe.IsAvailableAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", provider = StorageOptions.AzureBlobProvider })
        : Results.Json(new { status = "unavailable", provider = StorageOptions.AzureBlobProvider }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

// Email agent: mode, whether this instance leads, and the last successful mailbox poll. 503 when the agent is on without
// a configured mailbox, or when the leader hasn't polled successfully for EmailAgent:HealthFailureMinutes (failing,
// or never polled since it became leader). Other instances report 200 with leader=false. No counts, no addresses.
app.MapGet("/health/email", (IOptionsMonitor<EmailAgentOptions> emailOptions, IEmailAgentMonitor monitor) =>
{
    var o = emailOptions.CurrentValue;
    var lastSuccess = monitor.LastPollUtc ?? monitor.LeaderSinceUtc;
    var stale = monitor.IsLeader && lastSuccess is { } since
        && DateTimeOffset.UtcNow - since > TimeSpan.FromMinutes(o.HealthFailureMinutes);
    var healthy = o.Mode == EmailAgentMode.Off || (monitor.MailboxConfigured && !stale);
    var body = new { mode = o.Mode.ToString(), leader = monitor.IsLeader, lastPollUtc = monitor.LastPollUtc };
    return healthy ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.Run();

// Makes the entry point visible to WebApplicationFactory in CitadelIQ.Tests.
public partial class Program;
