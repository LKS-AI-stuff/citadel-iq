using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_create_the_schema_pgvector_extension_indexes_and_root_folder()
    {
        await using var app = await postgres.CreateAppAsync();

        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(*) FROM pg_extension WHERE extname = 'vector'"));

        Assert.Equal(3, await app.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('Folders','Documents','DocumentChunks')"));

        Assert.Equal("Home", await app.ScalarAsync<string>(
            "SELECT \"Name\" FROM \"Folders\" WHERE \"Id\" = '00000000-0000-0000-0000-000000000000'"));

        Assert.Equal(2, await app.ScalarAsync<long>("SELECT count(*) FROM \"VersionInfo\""));

        var hnsw = await app.ScalarAsync<string>("SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_DocumentChunks_Embedding'");
        Assert.Contains("hnsw", hnsw);
        Assert.Contains("vector_cosine_ops", hnsw);

        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(*) FROM pg_indexes WHERE indexname = 'UX_Folders_ParentFolderId_LowerName'"));
    }
}
