using Testcontainers.PostgreSql;

namespace CitadelIQ.Tests.Support;

/// <summary>One throwaway PostgreSQL + pgvector container shared by all integration tests; each test gets its
/// own database inside it (see <see cref="TestApp"/>). Requires Docker to be running.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public Task<TestApp> CreateAppAsync(double minSimilarity = 0.25, int chunkSize = 400, int chunkOverlap = 80) =>
        TestApp.CreateAsync(_container.GetConnectionString(), minSimilarity, chunkSize, chunkOverlap);
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
