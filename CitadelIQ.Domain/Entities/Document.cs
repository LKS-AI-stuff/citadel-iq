using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// Metadata for an uploaded document. Raw bytes live in <c>IDocumentStorage</c> (Infrastructure);
/// this entity only tracks metadata and processing state.
/// </summary>
public class Document
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid FolderId { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    /// <summary>The user who uploaded the document; kept after they leave so the UI can show "former member".</summary>
    public Guid? UploadedByUserId { get; private set; }

    private Document(
        Guid id,
        Guid workspaceId,
        Guid folderId,
        Guid? uploadedByUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset uploadedAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        FolderId = folderId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = ProcessingStatus.Uploaded;
    }

    /// <summary>Creates a document in <paramref name="folder"/>'s workspace.</summary>
    public static Document Create(Folder folder, Guid uploadedByUserId, string fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("File name cannot be empty.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("File is empty.");
        }

        return new Document(Guid.NewGuid(), folder.WorkspaceId, folder.Id, uploadedByUserId, fileName.Trim(), contentType, sizeBytes, DateTimeOffset.UtcNow);
    }

    public void AdvanceTo(ProcessingStatus status)
    {
        if (Status == ProcessingStatus.Failed)
        {
            return;
        }

        Status = status;
    }

    public void MarkFailed(string reason)
    {
        Status = ProcessingStatus.Failed;
        FailureReason = reason;
    }
}
