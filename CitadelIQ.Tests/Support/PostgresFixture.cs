using Testcontainers.PostgreSql;

namespace CitadelIQ.Tests.Support;

/// <summary>One throwaway PostgreSQL + pgvector container shared by all integration tests; each test gets its
/// own database inside it (see <see cref="TestApp"/>). Requires Docker to be running.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public Task<TestApp> CreateAppAsync(double minSimilarity = 0.25, int chunkSize = 400, int chunkOverlap = 80, double ragMinSimilarity = 0.30,
        Action<CitadelIQ.Application.Options.StorageOptions>? configureStorage = null, string storageProvider = CitadelIQ.Application.Options.StorageOptions.LocalDiskProvider) =>
        TestApp.CreateAsync(_container.GetConnectionString(), minSimilarity, chunkSize, chunkOverlap, ragMinSimilarity, configureStorage, storageProvider);
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
