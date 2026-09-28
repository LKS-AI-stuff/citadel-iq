using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// A folder in the document-management tree. A null <see cref="ParentFolderId"/> marks the
/// single root ("Home") folder.
/// </summary>
public class Folder
{
    /// <summary>Well-known id of the single root ("Home") folder, so clients can navigate to it
    /// without a lookup.</summary>
    public static readonly Guid RootId = Guid.Empty;

    private static readonly char[] InvalidNameChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid? ParentFolderId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Folder(Guid id, string name, Guid? parentFolderId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        ParentFolderId = parentFolderId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Folder CreateRoot() =>
        new(RootId, "Home", parentFolderId: null, DateTimeOffset.UtcNow);

    public static Folder Create(string name, Guid parentFolderId)
    {
        var trimmed = ValidateName(name);
        return new Folder(Guid.NewGuid(), trimmed, parentFolderId, DateTimeOffset.UtcNow);
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
