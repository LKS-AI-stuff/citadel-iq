using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class DocumentChunkRepository(CitadelIQDbContext db) : IDocumentChunkRepository
{
    public async Task AddRangeAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        db.DocumentChunks.AddRange(chunks);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default)
    {
        await db.DocumentChunks.Where(c => documentIds.Contains(c.DocumentId)).ExecuteDeleteAsync(cancellationToken);
    }
}
