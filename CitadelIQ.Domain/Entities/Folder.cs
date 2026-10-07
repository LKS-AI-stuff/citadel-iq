using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// A folder in a workspace's document tree. A null <see cref="ParentFolderId"/> marks the workspace's
/// root ("Home") folder — exactly one per workspace.
/// </summary>
public class Folder
{
    public const string RootName = "Home";

    private static readonly char[] InvalidNameChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; }
    public Guid? ParentFolderId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Folder(Guid id, Guid workspaceId, string name, Guid? parentFolderId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        ParentFolderId = parentFolderId;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsRoot => ParentFolderId is null;

    public static Folder CreateRoot(Guid workspaceId) =>
        new(Guid.NewGuid(), workspaceId, RootName, parentFolderId: null, DateTimeOffset.UtcNow);

    /// <summary>Creates a subfolder in the parent's workspace, so a child can never land in another workspace.</summary>
    public static Folder CreateChild(Folder parent, string name)
    {
        var trimmed = ValidateName(name);
        return new Folder(Guid.NewGuid(), parent.WorkspaceId, trimmed, parent.Id, DateTimeOffset.UtcNow);
    }

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Folder name cannot be empty.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > 255)
        {
            throw new DomainException("Folder name is too long.");
        }

        if (trimmed.IndexOfAny(InvalidNameChars) >= 0)
        {
            throw new DomainException("Folder name contains invalid characters.");
        }

        return trimmed;
    }
}
