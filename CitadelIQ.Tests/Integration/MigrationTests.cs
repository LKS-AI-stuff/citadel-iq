using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_create_the_schema_pgvector_extension_and_indexes()
    {
        await using var app = await postgres.CreateAppAsync();

        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(*) FROM pg_extension WHERE extname = 'vector'"));

        Assert.Equal(7, await app.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN " +
            "('Workspaces','Users','Memberships','JoinRequests','Folders','Documents','DocumentChunks')"));

        Assert.Equal(2, await app.ScalarAsync<long>("SELECT count(*) FROM \"VersionInfo\""));

        var hnsw = await app.ScalarAsync<string>("SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_DocumentChunks_Embedding'");
        Assert.Contains("hnsw", hnsw);
        Assert.Contains("vector_cosine_ops", hnsw);

        foreach (var index in new[] { "UX_Folders_ParentFolderId_LowerName", "UX_Folders_WorkspaceRoot", "UX_Users_Issuer_Subject", "UX_JoinRequests_PendingPerUser", "UX_Workspaces_JoinCode", "IX_DocumentChunks_WorkspaceId" })
        {
            Assert.Equal(1, await app.ScalarAsync<long>($"SELECT count(*) FROM pg_indexes WHERE indexname = '{index}'"));
        }
    }

    [Fact]
    public async Task There_is_no_global_home_folder_only_one_root_per_workspace()
    {
        await using var app = await postgres.CreateAppAsync();

        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(*) FROM \"Folders\" WHERE \"Id\" = '00000000-0000-0000-0000-000000000000'"));
        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(*) FROM \"Folders\" WHERE \"ParentFolderId\" IS NULL"));
        Assert.Equal("Home", await app.ScalarAsync<string>($"SELECT \"Name\" FROM \"Folders\" WHERE \"Id\" = '{app.Root}'"));
    }

    [Fact]
    public async Task Row_level_security_is_enabled_and_forced_on_every_content_table()
    {
        await using var app = await postgres.CreateAppAsync();

        var tables = await app.ColumnAsync(
            "SELECT relname FROM pg_class WHERE relrowsecurity AND relforcerowsecurity AND relname IN ('Folders','Documents','DocumentChunks') ORDER BY relname");

        Assert.Equal(["DocumentChunks", "Documents", "Folders"], tables.Cast<string>());
        Assert.Equal(3, await app.ScalarAsync<long>("SELECT count(*) FROM pg_policies WHERE policyname LIKE '%_workspace_isolation'"));
    }

    [Fact]
    public async Task The_runtime_role_has_data_rights_only()
    {
        await using var app = await postgres.CreateAppAsync();

        Assert.False(await app.ScalarAsync<bool>("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = 'citadeliq_app'"));
        Assert.True(await app.ScalarAsync<bool>("SELECT has_table_privilege('citadeliq_app', '\"Documents\"', 'SELECT, INSERT, UPDATE, DELETE')"));
        Assert.False(await app.ScalarAsync<bool>("SELECT has_table_privilege('citadeliq_app', '\"VersionInfo\"', 'INSERT')"));
    }
}
