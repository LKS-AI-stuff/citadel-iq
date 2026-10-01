using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>Deletes the chunks (and the embeddings stored on them) of the given documents.</summary>
    Task DeleteByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default);
}
