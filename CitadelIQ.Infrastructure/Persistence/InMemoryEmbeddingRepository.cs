// OBSOLETE: Replaced by EmbeddingRepository (PostgreSQL + pgvector via EF Core). Kept for reference only; not registered in DI.
using System.Collections.Concurrent;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Infrastructure.Persistence;

[Obsolete("Replaced by EmbeddingRepository (PostgreSQL + pgvector via EF Core). Kept for reference only; not registered in DI.")]
public class InMemoryEmbeddingRepository : IEmbeddingRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentEmbedding> _embeddings = new();

    public Task AddRangeAsync(IReadOnlyCollection<DocumentEmbedding> embeddings, CancellationToken cancellationToken = default)
    {
        foreach (var embedding in embeddings)
        {
            _embeddings[embedding.ChunkId] = embedding;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentEmbedding>> GetByChunkIdsAsync(IReadOnlyCollection<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentEmbedding> embeddings = chunkIds
            .Where(_embeddings.ContainsKey)
            .Select(id => _embeddings[id])
            .ToList();

        return Task.FromResult(embeddings);
    }

    public Task DeleteByChunkIdsAsync(IReadOnlyCollection<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        foreach (var chunkId in chunkIds)
        {
            _embeddings.TryRemove(chunkId, out _);
        }

        return Task.CompletedTask;
    }
}
