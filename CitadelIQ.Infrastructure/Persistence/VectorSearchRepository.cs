using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// pgvector similarity search. The query is metadata filtering (Ready documents, optional folder
/// set) followed by <c>ORDER BY embedding &lt;=&gt; @query LIMIT @topK</c>: <c>&lt;=&gt;</c> is pgvector's
/// cosine-distance operator (0 = identical direction), so similarity = 1 - distance. Ordering by the
/// bare distance expression lets Postgres use the HNSW index (vector_cosine_ops) when the planner
/// finds it cheaper than an exact scan; small or heavily filtered sets fall back to exact search.
/// </summary>
public class VectorSearchRepository(CitadelIQDbContext db) : IVectorSearchRepository
{
    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        float[] queryEmbedding,
        IReadOnlyCollection<Guid>? eligibleFolderIds,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(queryEmbedding);

        var candidates = db.DocumentChunks
            .AsNoTracking()
            .Join(db.Documents, chunk => chunk.DocumentId, document => document.Id, (chunk, document) => new { chunk, document })
            .Where(x => x.document.Status == ProcessingStatus.Ready
                        && EF.Property<Vector?>(x.chunk, DocumentChunkConfiguration.EmbeddingProperty) != null);

        if (eligibleFolderIds is not null)
        {
            candidates = candidates.Where(x => eligibleFolderIds.Contains(x.document.FolderId));
        }

        var rows = await candidates
            .Select(x => new
            {
                ChunkId = x.chunk.Id,
                DocumentId = x.document.Id,
                x.document.FolderId,
                x.document.FileName,
                x.document.ContentType,
                x.chunk.Text,
                x.chunk.ChunkIndex,
                x.chunk.PageNumber,
                x.chunk.SheetName,
                Distance = EF.Property<Vector>(x.chunk, DocumentChunkConfiguration.EmbeddingProperty).CosineDistance(queryVector)
            })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DocumentSearchResult(
                r.ChunkId, r.DocumentId, r.FolderId, r.FileName, r.ContentType, r.Text, r.ChunkIndex, r.PageNumber, r.SheetName, 1d - r.Distance))
            .ToList();
    }
}
