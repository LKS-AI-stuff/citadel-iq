using CitadelIQ.Application.Common;
using CitadelIQ.Application.Folders;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class FolderTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Sibling_folder_names_must_be_unique_ignoring_case_but_can_repeat_under_other_parents()
    {
        await using var app = await postgres.CreateAppAsync();
        var a = await app.CreateFolderAsync(app.Root, "A");
        await app.CreateFolderAsync(app.Root, "Reports");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => app.CreateFolderAsync(app.Root, "reports"));
        Assert.Equal("A folder named \"reports\" already exists here.", ex.Message);

        await app.CreateFolderAsync(a.Id, "Reports"); // different parent: fine
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_name_yield_exactly_one_folder()
    {
        await using var app = await postgres.CreateAppAsync();

        async Task<bool> TryCreate()
        {
            try
            {
                await app.CreateFolderAsync(app.Root, "Race");
                return true;
            }
            catch (ValidationException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(TryCreate(), TryCreate(), TryCreate());

        Assert.Equal(1, outcomes.Count(created => created));
        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(*) FROM \"Folders\" WHERE \"Name\" = 'Race'"));
    }

    [Fact]
    public async Task Rename_rejects_a_sibling_collision_but_allows_a_case_only_change()
    {
        await using var app = await postgres.CreateAppAsync();
        var one = await app.CreateFolderAsync(app.Root, "One");
        await app.CreateFolderAsync(app.Root, "Two");

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.RunAsync(sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(one.Id, "two")));

        var renamed = await app.RunAsync(sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(one.Id, "ONE"));
        Assert.Equal("ONE", renamed.Name);
    }

    [Fact]
    public async Task The_home_folder_cannot_be_renamed_or_deleted()
    {
        await using var app = await postgres.CreateAppAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.RunAsync(sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(app.Root, "Elsewhere")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            app.RunAsync(sp => sp.GetRequiredService<IFolderService>().DeleteFolderAsync(app.Root)));
    }

    [Fact]
    public async Task Deleting_a_folder_removes_its_whole_subtree_documents_chunks_and_files()
    {
        await using var app = await postgres.CreateAppAsync();
        var finance = await app.CreateFolderAsync(app.Root, "Finance");
        var reports = await app.CreateFolderAsync(finance.Id, "Reports");
        var keep = await app.CreateFolderAsync(app.Root, "Keep");
        var topDoc = await app.UploadAndProcessAsync(finance.Id, "top.txt", TestFiles.Text("revenue"));
        var deepDoc = await app.UploadAndProcessAsync(reports.Id, "deep.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(keep.Id, "kept.txt", TestFiles.Text("vacation"));

        await app.RunAsync(sp => sp.GetRequiredService<IFolderService>().DeleteFolderAsync(finance.Id));

        Assert.Equal(2, await app.ScalarAsync<long>("SELECT count(*) FROM \"Folders\"")); // Home + Keep
        Assert.Equal(["kept.txt"], (await app.ColumnAsync("SELECT \"FileName\" FROM \"Documents\"")).Cast<string>());
        Assert.Equal(1, await app.ScalarAsync<long>("SELECT count(DISTINCT \"DocumentId\") FROM \"DocumentChunks\""));
        Assert.False(File.Exists(app.StoredFilePath(app.DefaultWorkspaceId, topDoc, ".txt")));
        Assert.False(File.Exists(app.StoredFilePath(app.DefaultWorkspaceId, deepDoc, ".txt")));
    }

    [Fact]
    public async Task Folder_contents_list_subfolders_documents_and_the_breadcrumb()
    {
        await using var app = await postgres.CreateAppAsync();
        var finance = await app.CreateFolderAsync(app.Root, "Finance");
        var reports = await app.CreateFolderAsync(finance.Id, "Reports");
        await app.UploadAsync(finance.Id, "budget.txt", TestFiles.Text("x"));

        var contents = await app.RunAsync(sp => sp.GetRequiredService<IFolderService>().GetContentsAsync(finance.Id));

        Assert.Equal(["Reports"], contents.Subfolders.Select(f => f.Name));
        Assert.Equal(["budget.txt"], contents.Documents.Select(d => d.FileName));
        Assert.Equal(["Home", "Finance"], contents.FolderPath.Select(s => s.Name));
        _ = reports;
    }
}
