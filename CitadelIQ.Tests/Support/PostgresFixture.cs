using Npgsql;
using Testcontainers.PostgreSql;

namespace CitadelIQ.Tests.Support;

/// <summary>One throwaway PostgreSQL + pgvector container shared by all integration tests; each test gets its
/// own database inside it (see <see cref="TestApp"/>). Requires Docker to be running.
/// The container's default user is a superuser (used for migrations and for inspecting data); the restricted runtime
/// role <see cref="AppRole"/> is created once here, exactly as an operator would, so row-level security is really
/// exercised by every test.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppRole = "citadeliq_app";
    public const string AppRolePassword = "app-test-password";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithCommand("-c", "max_connections=300")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"CREATE ROLE {AppRole} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public Task<TestApp> CreateAppAsync(double minSimilarity = 0.25, int chunkSize = 400, int chunkOverlap = 80, double ragMinSimilarity = 0.30,
        Action<CitadelIQ.Application.Options.StorageOptions>? configureStorage = null, string storageProvider = CitadelIQ.Application.Options.StorageOptions.LocalDiskProvider) =>
        TestApp.CreateAsync(AdminConnectionString, minSimilarity, chunkSize, chunkOverlap, ragMinSimilarity, configureStorage, storageProvider);
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
