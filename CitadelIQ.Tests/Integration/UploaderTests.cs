using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Organizations;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class UploaderTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Documents_search_results_and_answer_sources_show_who_uploaded_them()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Mia Member");
        app.Chat.StreamDeltas = ["ok [1]"];

        var uploaded = await app.UploadAsync(root, "budget.txt", TestFiles.Text("revenue"), member);
        await app.ProcessAsync(uploaded.Id, member);

        var contents = await app.RunAsAsync(owner, sp => sp.GetRequiredService<IFolderService>().GetContentsAsync(root));
        var result = Assert.Single(await app.SearchAsync("revenue", root, SearchScope.EntireWorkspace, user: owner));
        var (run, _) = await app.AskAsync(new AskRequestDto("revenue?", root, SearchScope.EntireWorkspace, []), owner);

        var expected = new UploaderDto("Mia Member", IsFormerMember: false);
        Assert.Equal(expected, uploaded.UploadedBy);
        Assert.Equal(expected, Assert.Single(contents.Documents).UploadedBy);
        Assert.Equal(expected, result.UploadedBy);
        Assert.Equal(expected, Assert.Single(run.Sources).UploadedBy);
    }

    [Fact]
    public async Task Documents_of_a_removed_member_show_them_as_a_former_member()
    {
        await using var app = await postgres.CreateAppAsync();
        var (owner, _, root) = await app.CreateOrganizationWithOwnerAsync("Acme");
        var member = await app.AddMemberAsync(owner, "Mia Member");
        await app.UploadAndProcessAsync(root, "budget.txt", TestFiles.Text("revenue"), member);

        await app.RunAsAsync(owner, sp => sp.GetRequiredService<IOrganizationAdminService>().RemoveMemberAsync(member.Id));

        var contents = await app.RunAsAsync(owner, sp => sp.GetRequiredService<IFolderService>().GetContentsAsync(root));
        var result = Assert.Single(await app.SearchAsync("revenue", root, SearchScope.EntireWorkspace, user: owner));

        Assert.Equal(new UploaderDto("Mia Member", IsFormerMember: true), Assert.Single(contents.Documents).UploadedBy);
        Assert.True(result.UploadedBy!.IsFormerMember);
    }
}
