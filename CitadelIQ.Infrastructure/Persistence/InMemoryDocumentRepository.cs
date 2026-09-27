using System.Collections.Concurrent;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// Global, app-wide in-memory document metadata store. Thread-safe singleton; replace with a
/// database-backed implementation later without touching the Application layer.
/// </summary>
public class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _documents = new();

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.GetValueOrDefault(id));

    public Task<IReadOnlyList<Document>> GetByFolderIdAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> documents = _documents.Values
            .Where(d => d.FolderId == folderId)
            .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(documents);
    }

    public Task<IReadOnlyList<Document>> GetByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        var folderIdSet = folderIds.ToHashSet();
        IReadOnlyList<Document> documents = _documents.Values
            .Where(d => folderIdSet.Contains(d.FolderId))
            .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(documents);
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }
}
