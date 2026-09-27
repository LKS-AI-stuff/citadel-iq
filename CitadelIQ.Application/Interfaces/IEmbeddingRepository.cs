using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IEmbeddingRepository
{
    Task AddRangeAsync(IReadOnlyCollection<DocumentEmbedding> embeddings, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentEmbedding>> GetByChunkIdsAsync(IReadOnlyCollection<Guid> chunkIds, CancellationToken cancellationToken = default);
}
