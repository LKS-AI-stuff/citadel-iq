using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// The domain models embeddings as their own entity, but physically the vector is a column on the
/// chunk's row in <c>DocumentChunks</c> — a vector search then needs no join to reach it.
/// </summary>
public class EmbeddingRepository(CitadelIQDbContext db) : IEmbeddingRepository
{
    public async Task AddRangeAsync(IReadOnlyCollection<DocumentEmbedding> embeddings, CancellationToken cancellationToken = default)
    {
        var embeddingsByChunkId = embeddings.ToDictionary(e => e.ChunkId);
        var chunkIds = embeddingsByChunkId.Keys.ToList();

        var chunks = await db.DocumentChunks.Where(c => chunkIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (chunks.Count != chunkIds.Count)
        {
            throw new InvalidOperationException("Cannot store embeddings for chunks that were not persisted.");
        }

        foreach (var chunk in chunks)
        {
            var embedding = embeddingsByChunkId[chunk.Id];
            var entry = db.Entry(chunk);
            entry.Property<Vector?>(DocumentChunkConfiguration.EmbeddingProperty).CurrentValue = new Vector(embedding.Vector);
            entry.Property<string?>(DocumentChunkConfiguration.ModelNameProperty).CurrentValue = embedding.ModelName;
        }

        // One SaveChanges = one transaction: either every chunk gets its embedding or none does.
        await db.SaveChangesAsync(cancellationToken);
    }
}
