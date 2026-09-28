using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Interfaces;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default);
}
