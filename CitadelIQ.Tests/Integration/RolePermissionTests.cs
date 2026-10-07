using CitadelIQ.Application.Common;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class RolePermissionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Members_browse_upload_create_folders_search_and_ask()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Member");
        app.Chat.StreamDeltas = ["ok [1]"];

        var folder = await app.CreateFolderAsync(root, "Reports", member);
        await app.UploadAndProcessAsync(folder.Id, "report.txt", TestFiles.Text("revenue"), member);
        var results = await app.SearchAsync("revenue", root, SearchScope.EntireWorkspace, user: member);
        var (run, _) = await app.AskAsync(new AskRequestDto("revenue?", root, SearchScope.EntireWorkspace, []), member);

        Assert.Equal("report.txt", Assert.Single(results).FileName);
        Assert.Single(run.Sources);
    }

    [Fact]
    public async Task Members_cannot_rename_or_delete_but_admins_and_owners_can()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var admin = await app.AddMemberAsync(owner, "Admin", WorkspaceRole.Admin);
        var member = await app.AddMemberAsync(owner, "Member");
        var folder = await app.CreateFolderAsync(root, "Reports", member);
        var document = await app.UploadAsync(folder.Id, "report.txt", TestFiles.Text("revenue"), member);

        Task AsUser(TestUser user, Func<IServiceProvider, Task> action) => app.RunAsAsync(user, action);

        var rename = await Assert.ThrowsAsync<ForbiddenException>(() => AsUser(member, sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(folder.Id, "X")));
        Assert.Equal("You don't have permission to rename folders.", rename.Message);
        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser(member, sp => sp.GetRequiredService<IFolderService>().DeleteFolderAsync(folder.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser(member, sp => sp.GetRequiredService<IDocumentService>().DeleteDocumentAsync(document.Id)));

        await AsUser(admin, sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(folder.Id, "Renamed"));
        await AsUser(admin, sp => sp.GetRequiredService<IDocumentService>().DeleteDocumentAsync(document.Id));
        await AsUser(owner, sp => sp.GetRequiredService<IFolderService>().DeleteFolderAsync(folder.Id));

        Assert.Equal(1, await app.ScalarAsync<long>($"SELECT count(*) FROM \"Folders\" WHERE \"WorkspaceId\" = (SELECT \"WorkspaceId\" FROM \"Folders\" WHERE \"Id\" = '{root}')"));
    }

    [Fact]
    public async Task The_owner_of_an_individual_workspace_can_do_everything()
    {
        await using var app = await postgres.CreateAppAsync();
        var folder = await app.CreateFolderAsync(app.Root, "Mine");
        var document = await app.UploadAsync(folder.Id, "mine.txt", TestFiles.Text("x"));

        await app.RunAsync(sp => sp.GetRequiredService<IFolderService>().RenameFolderAsync(folder.Id, "Still mine"));
        await app.RunAsync(sp => sp.GetRequiredService<IDocumentService>().DeleteDocumentAsync(document.Id));
        await app.RunAsync(sp => sp.GetRequiredService<IFolderService>().DeleteFolderAsync(folder.Id));
    }
}
