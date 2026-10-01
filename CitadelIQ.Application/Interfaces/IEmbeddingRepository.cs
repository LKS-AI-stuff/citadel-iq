using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IEmbeddingRepository
{
    /// <summary>Attaches embeddings to already-persisted chunks. Embeddings are removed together
    /// with their chunks (<see cref="IDocumentChunkRepository.DeleteByDocumentIdsAsync"/>).</summary>
    Task AddRangeAsync(IReadOnlyCollection<DocumentEmbedding> embeddings, CancellationToken cancellationToken = default);
}
