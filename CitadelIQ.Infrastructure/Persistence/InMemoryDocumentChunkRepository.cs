using System.Collections.Concurrent;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Infrastructure.Persistence;

public class InMemoryDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentChunk> _chunks = new();

    public Task AddRangeAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            _chunks[chunk.Id] = chunk;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default)
    {
        var documentIdSet = documentIds.ToHashSet();
        IReadOnlyList<DocumentChunk> chunks = _chunks.Values
            .Where(c => documentIdSet.Contains(c.DocumentId))
            .ToList();

        return Task.FromResult(chunks);
    }
}
