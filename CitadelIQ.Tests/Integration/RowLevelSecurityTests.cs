using CitadelIQ.Tests.Support;
using Npgsql;

namespace CitadelIQ.Tests.Integration;

/// <summary>The database layer alone — no EF query filters — keeps workspaces apart for the runtime role.</summary>
[Collection(PostgresCollection.Name)]
public class RowLevelSecurityTests(PostgresFixture postgres)
{
    private static async Task<long> CountAsAppRoleAsync(TestApp app, string table, string workspaceSetting)
    {
        await using var connection = new NpgsqlConnection(app.AppConnectionString);
        await connection.OpenAsync();
        await using (var set = new NpgsqlCommand("SELECT set_config('app.workspace_id', @w, false)", connection))
        {
            set.Parameters.AddWithValue("w", workspaceSetting);
            await set.ExecuteNonQueryAsync();
        }

        await using var count = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\"", connection);
        return (long)(await count.ExecuteScalarAsync())!;
    }

    private static async Task<(TestApp App, Guid BWorkspace, Guid BFolder)> SetUpAsync(PostgresFixture postgres)
    {
        var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(app.Root, "a.txt", TestFiles.Text("revenue"));

        var b = await app.CreateUserAsync("Other Tenant");
        var bSession = await app.OnboardIndividualAsync(b);
        var bFolder = await app.CreateFolderAsync(bSession.Workspace!.RootFolderId, "B", b);
        await app.UploadAndProcessAsync(bFolder.Id, "b1.txt", TestFiles.Text("revenue"), b);
        await app.UploadAndProcessAsync(bFolder.Id, "b2.txt", TestFiles.Text("vacation"), b);
        return (app, bSession.Workspace.Id, bFolder.Id);
    }

    [Theory]
    [InlineData("Folders", 1, 2)]
    [InlineData("Documents", 1, 2)]
    [InlineData("DocumentChunks", 1, 2)]
    public async Task The_runtime_role_sees_only_the_stamped_workspace(string table, long expectedA, long expectedB)
    {
        var (app, bWorkspace, _) = await SetUpAsync(postgres);
        await using var _ = app;

        Assert.Equal(expectedA, await CountAsAppRoleAsync(app, table, app.DefaultWorkspaceId.ToString()));
        Assert.Equal(expectedB, await CountAsAppRoleAsync(app, table, bWorkspace.ToString()));
        Assert.Equal(0, await CountAsAppRoleAsync(app, table, ""));          // no workspace: fail closed
        Assert.Equal(expectedA + expectedB, await app.ScalarAsync<long>($"SELECT count(*) FROM \"{table}\"")); // superuser sees all
    }

    [Fact]
    public async Task The_runtime_role_cannot_write_rows_into_another_workspace()
    {
        var (app, bWorkspace, bFolder) = await SetUpAsync(postgres);
        await using var _ = app;

        await using var connection = new NpgsqlConnection(app.AppConnectionString);
        await connection.OpenAsync();
        await using (var set = new NpgsqlCommand($"SELECT set_config('app.workspace_id', '{app.DefaultWorkspaceId}', false)", connection))
        {
            await set.ExecuteNonQueryAsync();
        }

        await using var insert = new NpgsqlCommand(
            $"INSERT INTO \"Folders\" (\"Id\", \"WorkspaceId\", \"Name\", \"ParentFolderId\", \"CreatedAtUtc\") VALUES (gen_random_uuid(), '{bWorkspace}', 'Injected', '{bFolder}', now())",
            connection);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState); // "new row violates row-level security policy"

        await using var update = new NpgsqlCommand("UPDATE \"Documents\" SET \"FileName\" = 'renamed'", connection);
        Assert.Equal(1, await update.ExecuteNonQueryAsync()); // only A's single document is even visible to update
        Assert.Equal(0, await app.ScalarAsync<long>($"SELECT count(*) FROM \"Documents\" WHERE \"WorkspaceId\" = '{bWorkspace}' AND \"FileName\" = 'renamed'"));
    }

    [Fact]
    public async Task Composite_keys_stop_a_document_being_attached_to_another_workspaces_folder()
    {
        var (app, _, bFolder) = await SetUpAsync(postgres);
        await using var _ = app;

        // Even the superuser (no RLS) cannot attach A's document to B's folder.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => app.ExecuteAsync(
            $"INSERT INTO \"Documents\" (\"Id\", \"WorkspaceId\", \"FolderId\", \"FileName\", \"ContentType\", \"SizeBytes\", \"UploadedAtUtc\", \"ProcessingStatus\") " +
            $"VALUES (gen_random_uuid(), '{app.DefaultWorkspaceId}', '{bFolder}', 'x.txt', 'text/plain', 1, now(), 'Uploaded')"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task A_workspace_can_have_only_one_root_folder()
    {
        await using var app = await postgres.CreateAppAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => app.ExecuteAsync(
            $"INSERT INTO \"Folders\" (\"Id\", \"WorkspaceId\", \"Name\", \"ParentFolderId\", \"CreatedAtUtc\") VALUES (gen_random_uuid(), '{app.DefaultWorkspaceId}', 'Home2', NULL, now())"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }
}
