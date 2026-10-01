using System.Text.Json.Serialization;
using CitadelIQ.Api.Middleware;
using CitadelIQ.Application;
using CitadelIQ.Application.Options;
using CitadelIQ.FluentMigrations;
using CitadelIQ.Infrastructure;

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
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

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

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
