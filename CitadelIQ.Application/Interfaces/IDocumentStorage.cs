namespace CitadelIQ.Application.Interfaces;

/// <summary>
/// Stores raw uploaded file bytes. Current implementation writes to local disk (see PLAN.md §3);
/// a future implementation can move this to object storage or a DB blob column without any
/// Application-layer change.
/// </summary>
public interface IDocumentStorage
{
    Task SaveAsync(Guid documentId, string fileExtension, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(Guid documentId, string fileExtension, CancellationToken cancellationToken = default);
}
