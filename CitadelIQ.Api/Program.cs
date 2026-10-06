using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CitadelIQ.Api.Middleware;
using CitadelIQ.Application;
using CitadelIQ.Application.Options;
using CitadelIQ.FluentMigrations;
using CitadelIQ.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection("Chunking"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddOptions<StorageOptions>().Bind(builder.Configuration.GetSection("Storage")).ValidateOnStart();
builder.Services.AddOptions<RagOptions>().Bind(builder.Configuration.GetSection("Rag")).ValidateOnStart();

// Per-IP limits on /api/answers only: a request window plus a concurrent-stream cap (an SSE response holds its
// permit until the stream ends). Everything else is unlimited.
var rateLimit = builder.Configuration.GetSection("Rag:RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/api/answers")
                ? RateLimitPartition.GetConcurrencyLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new ConcurrencyLimiterOptions { PermitLimit = rateLimit.MaxConcurrentStreams, QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("other")),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/api/answers")
                ? RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimit.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimit.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0
                    })
                : RateLimitPartition.GetNoLimiter("other")));
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
            Title = "You're asking questions too quickly. Please wait a moment and try again."
        }, options: null, contentType: "application/problem+json", cancellationToken);
    };
});

// Off by default. Behind a reverse proxy, list its address(es) so the rate limiter sees the real client IP.
var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
if (knownProxies.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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

builder.Services.AddFluentMigrations(connectionString);

var app = builder.Build();

// First in the pipeline so redirection, rate limiting and logging see the real client address/scheme.
if (knownProxies.Length > 0)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enabled in appsettings.Development.json; in deployed environments set Database__MigrateOnStartup=true
// (e.g. in docker-compose) or run the migrations as a separate step.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    app.Services.ApplyDatabaseMigrations();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

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
});

app.Run();

// Makes the entry point visible to WebApplicationFactory in CitadelIQ.Tests.
public partial class Program;
