using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Documents;

public interface IDocumentService
{
    Task<DocumentSummaryDto> UploadDocumentAsync(
        Guid folderId,
        string fileName,
        string contentType,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>Runs the extract/chunk/embed pipeline for an already-uploaded document. Called by
    /// the dispatcher on a background scope, not directly from a controller.</summary>
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<DocumentStatusDto> GetStatusAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<(Stream Content, string FileName, string ContentType)> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default);
}
