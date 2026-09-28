using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Entities;

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

    /// <summary>Deletes a document along with its stored file, chunks, and embeddings.</summary>
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Deletes several already-loaded documents (and their files, chunks, and embeddings)
    /// in one pass. Used by folder-cascade deletion to avoid an N+1 lookup per document.</summary>
    Task DeleteDocumentsAsync(IReadOnlyCollection<Document> documents, CancellationToken cancellationToken = default);
}
