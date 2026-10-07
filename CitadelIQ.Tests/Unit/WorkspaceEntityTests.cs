using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;
using CitadelIQ.Domain.Rules;

namespace CitadelIQ.Tests.Unit;

public class WorkspaceEntityTests
{
    [Fact]
    public void Organizations_have_a_join_code_and_individual_workspaces_do_not()
    {
        var org = Workspace.CreateOrganization("  Acme  ", "7K3MQ9TX2HDB");
        var individual = Workspace.CreateIndividual("Jane");

        Assert.Equal(WorkspaceKind.Organization, org.Kind);
        Assert.Equal("Acme", org.Name);
        Assert.Equal("7K3MQ9TX2HDB", org.JoinCode);
        Assert.Null(individual.JoinCode);
        Assert.Throws<DomainException>(() => individual.RegenerateJoinCode(JoinCode.Generate()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Organization_names_need_two_characters(string name)
    {
        Assert.Throws<DomainException>(() => Workspace.CreateOrganization(name, JoinCode.Generate()));
    }

    [Fact]
    public void An_individual_workspace_never_fails_on_a_short_display_name()
    {
        Assert.Equal("My workspace", Workspace.CreateIndividual("J").Name);
        Assert.Equal("Jo", Workspace.CreateIndividual(" Jo ").Name);
    }

    [Fact]
    public void Long_names_are_cut_to_the_maximum()
    {
        Assert.Equal(Workspace.MaxNameLength, Workspace.CreateIndividual(new string('x', 300)).Name.Length);
    }

    [Fact]
    public void A_child_folder_and_its_documents_and_chunks_inherit_the_workspace()
    {
        var workspaceId = Guid.NewGuid();
        var root = Folder.CreateRoot(workspaceId);
        var child = Folder.CreateChild(root, "Reports");
        var document = Document.Create(child, Guid.NewGuid(), "a.txt", "text/plain", 10);
        var chunk = DocumentChunk.Create(document, 0, "text");

        Assert.True(root.IsRoot);
        Assert.Equal(Folder.RootName, root.Name);
        Assert.False(child.IsRoot);
        Assert.Equal(root.Id, child.ParentFolderId);
        Assert.Equal(workspaceId, child.WorkspaceId);
        Assert.Equal(workspaceId, document.WorkspaceId);
        Assert.Equal(child.Id, document.FolderId);
        Assert.Equal(workspaceId, chunk.WorkspaceId);
        Assert.Equal(document.Id, chunk.DocumentId);
    }

    [Fact]
    public void Join_requests_can_be_decided_once()
    {
        var request = JoinRequest.Create(Guid.NewGuid(), Guid.NewGuid());
        var admin = Guid.NewGuid();

        request.Approve(admin);

        Assert.Equal(JoinRequestStatus.Approved, request.Status);
        Assert.Equal(admin, request.DecidedByUserId);
        Assert.NotNull(request.DecidedAtUtc);
        Assert.Throws<DomainException>(() => request.Reject(admin));
        Assert.Throws<DomainException>(() => request.Cancel());
    }

    [Fact]
    public void Users_fall_back_to_their_email_as_display_name_and_close_once()
    {
        var user = UserAccount.Create("iss", "sub", " jane@example.test ", null);

        Assert.Equal("jane@example.test", user.Email);
        Assert.Equal("jane@example.test", user.DisplayName);

        user.RecordSignIn(null, "Jane Doe");
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Equal("jane@example.test", user.Email); // blank claims don't wipe the profile

        user.Close();
        var closedAt = user.ClosedAtUtc;
        user.Close();
        Assert.True(user.IsClosed);
        Assert.Equal(closedAt, user.ClosedAtUtc);
    }
}
