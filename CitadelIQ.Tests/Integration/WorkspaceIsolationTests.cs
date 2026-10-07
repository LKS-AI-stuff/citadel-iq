using CitadelIQ.Application.Common;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

/// <summary>
/// Two individual workspaces, A (the default user) and B, holding the same kind of content. Every service entry
/// point used as A must treat B's ids as nonexistent and never return B's data.
/// </summary>
[Collection(PostgresCollection.Name)]
public class WorkspaceIsolationTests(PostgresFixture postgres)
{
    private sealed record Fixture(TestApp App, TestUser B, Guid BRoot, Guid BFolder, Guid BDocument, Guid AFolder, Guid ADocument);

    private async Task<Fixture> SetUpAsync()
    {
        var app = await postgres.CreateAppAsync(minSimilarity: 0);

        var b = await app.CreateUserAsync("Other Tenant");
        var bRoot = (await app.OnboardIndividualAsync(b)).Workspace!.RootFolderId;
        var bFolder = await app.CreateFolderAsync(bRoot, "Finance", b);
        // B's document is the *exact* match for "revenue"; A's is only partial, so a leak would rank first.
        var bDocument = await app.UploadAndProcessAsync(bFolder.Id, "b-secret.txt", TestFiles.Text("revenue"), b);

        var aFolder = await app.CreateFolderAsync(app.Root, "Finance");
        var aDocument = await app.UploadAndProcessAsync(aFolder.Id, "a.txt", TestFiles.Text("revenue vacation"));

        return new Fixture(app, b, bRoot, bFolder.Id, bDocument, aFolder.Id, aDocument);
    }

    [Fact]
    public async Task Another_workspaces_folders_are_not_found()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;

        Task Folders(Func<IFolderService, Task> action) => f.App.RunAsync(sp => action(sp.GetRequiredService<IFolderService>()));

        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.GetByIdAsync(f.BFolder)));
        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.GetContentsAsync(f.BFolder)));
        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.GetContentsAsync(f.BRoot)));
        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.CreateFolderAsync(f.BFolder, "Injected")));
        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.RenameFolderAsync(f.BFolder, "Renamed")));
        await Assert.ThrowsAsync<NotFoundException>(() => Folders(s => s.DeleteFolderAsync(f.BFolder)));

        // B's tree is untouched.
        var bContents = await f.App.RunAsAsync(f.B, sp => sp.GetRequiredService<IFolderService>().GetContentsAsync(f.BFolder));
        Assert.Equal("Finance", bContents.Folder.Name);
        Assert.Empty(bContents.Subfolders);
    }

    [Fact]
    public async Task Another_workspaces_documents_are_not_found()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;

        Task Documents(Func<IDocumentService, Task> action) => f.App.RunAsync(sp => action(sp.GetRequiredService<IDocumentService>()));

        await Assert.ThrowsAsync<NotFoundException>(() => Documents(s => s.GetStatusAsync(f.BDocument)));
        await Assert.ThrowsAsync<NotFoundException>(() => Documents(s => s.DownloadAsync(f.BDocument)));
        await Assert.ThrowsAsync<NotFoundException>(() => Documents(s => s.DeleteDocumentAsync(f.BDocument)));
        await Assert.ThrowsAsync<NotFoundException>(() => f.App.UploadAsync(f.BFolder, "injected.txt", TestFiles.Text("x")));

        Assert.Equal(ProcessingStatus.Ready, (await f.App.GetStatusAsync(f.BDocument, f.B)).Status);
        Assert.True(File.Exists(f.App.StoredFilePath((await f.App.SessionAsync(f.B)).Workspace!.Id, f.BDocument, ".txt")));
    }

    [Fact]
    public async Task Entire_workspace_search_never_returns_another_workspaces_chunks()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;

        var results = await f.App.SearchAsync("revenue", f.App.Root, SearchScope.EntireWorkspace);

        Assert.Equal(["a.txt"], results.Select(r => r.FileName));
        Assert.Equal(0.707, results[0].SimilarityScore, 3); // B's exact (1.0) match was invisible
    }

    [Fact]
    public async Task Scoped_searches_reject_another_workspaces_folder()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;

        await Assert.ThrowsAsync<NotFoundException>(() => f.App.SearchAsync("revenue", f.BFolder, SearchScope.CurrentFolder));
        await Assert.ThrowsAsync<NotFoundException>(() => f.App.SearchAsync("revenue", f.BRoot, SearchScope.CurrentFolderAndSubfolders));
        await Assert.ThrowsAsync<NotFoundException>(() => f.App.SearchAsync("revenue", f.BRoot, SearchScope.EntireWorkspace));
    }

    [Fact]
    public async Task Ask_uses_only_the_callers_workspace_as_context()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;
        f.App.Chat.StreamDeltas = ["ok [1]"];

        var (run, _) = await f.App.AskAsync(new AskRequestDto("revenue?", f.App.Root, SearchScope.EntireWorkspace, []));

        Assert.Equal(["a.txt"], run.Sources.Select(s => s.FileName));
        var prompt = string.Join("\n", f.App.Chat.StreamCalls.Single().Select(m => m.Content));
        Assert.DoesNotContain("b-secret", prompt);
    }

    [Fact]
    public async Task Background_processing_only_sees_the_workspace_it_entered()
    {
        await using var app = await postgres.CreateAppAsync();
        var b = await app.CreateUserAsync("Other Tenant");
        var bSession = await app.OnboardIndividualAsync(b);
        var bDocument = await app.UploadAsync(bSession.Workspace!.RootFolderId, "b.txt", TestFiles.Text("revenue"), b);

        // Processing B's document from A's workspace finds nothing to process.
        await app.RunInWorkspaceAsync(app.DefaultWorkspaceId, sp => sp.GetRequiredService<IDocumentService>().ProcessDocumentAsync(bDocument.Id));
        Assert.Equal(ProcessingStatus.Uploaded, (await app.GetStatusAsync(bDocument.Id, b)).Status);

        // In B's workspace it is processed, and every chunk carries B's workspace id.
        await app.RunInWorkspaceAsync(bSession.Workspace.Id, sp => sp.GetRequiredService<IDocumentService>().ProcessDocumentAsync(bDocument.Id));
        Assert.Equal(ProcessingStatus.Ready, (await app.GetStatusAsync(bDocument.Id, b)).Status);
        Assert.Equal([bSession.Workspace.Id], (await app.ColumnAsync("SELECT DISTINCT \"WorkspaceId\" FROM \"DocumentChunks\"")).Cast<Guid>());
    }

    [Fact]
    public async Task Each_workspace_gets_its_own_home_folder_and_files_directory()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;

        var aRoot = await f.App.RunAsync(sp => sp.GetRequiredService<IFolderService>().GetRootAsync());
        var bRoot = await f.App.RunAsAsync(f.B, sp => sp.GetRequiredService<IFolderService>().GetRootAsync());

        Assert.NotEqual(aRoot.Id, bRoot.Id);
        Assert.Equal("Home", aRoot.Name);
        Assert.Equal("Home", bRoot.Name);
        Assert.True(File.Exists(f.App.StoredFilePath(f.App.DefaultWorkspaceId, f.ADocument, ".txt")));
    }

    [Fact]
    public async Task A_user_without_a_workspace_sees_no_content()
    {
        var f = await SetUpAsync();
        await using var _ = f.App;
        var newcomer = await f.App.CreateUserAsync("Newcomer");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            f.App.RunAsAsync(newcomer, sp => sp.GetRequiredService<IFolderService>().GetRootAsync()));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            f.App.RunAsAsync(newcomer, sp => sp.GetRequiredService<IFolderService>().GetContentsAsync(f.App.Root)));
    }
}
