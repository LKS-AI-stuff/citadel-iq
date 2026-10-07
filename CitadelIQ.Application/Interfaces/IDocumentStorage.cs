namespace CitadelIQ.Application.Interfaces;

/// <summary>
/// Stores raw uploaded file bytes (local disk or Azure Blob, by configuration). Files are keyed by workspace and
/// document — <c>{workspaceId}/{documentId}{extension}</c> — and are only ever served through the API's checks.
/// </summary>
public interface IDocumentStorage
{
    Task SaveAsync(Guid workspaceId, Guid documentId, string fileExtension, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(Guid workspaceId, Guid documentId, string fileExtension, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid workspaceId, Guid documentId, string fileExtension, CancellationToken cancellationToken = default);
}
