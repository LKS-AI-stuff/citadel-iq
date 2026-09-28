using System.Collections.Concurrent;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary>
/// Global, app-wide in-memory folder store. Thread-safe singleton; replace with a
/// database-backed implementation later without touching the Application layer.
/// </summary>
public class InMemoryFolderRepository : IFolderRepository
{
    private readonly ConcurrentDictionary<Guid, Folder> _folders = new();

    public InMemoryFolderRepository()
    {
        var root = Folder.CreateRoot();
        _folders[root.Id] = root;
    }

    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_folders.GetValueOrDefault(id));

    public Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Folder> children = _folders.Values
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(children);
    }

    public Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var descendants = new List<Guid>();
        CollectDescendants(folderId, descendants);
        return Task.FromResult<IReadOnlyList<Guid>>(descendants);
    }

    public Task<bool> ExistsWithNameAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var exists = _folders.Values.Any(f =>
            f.ParentFolderId == parentFolderId &&
            string.Equals(f.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task AddAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        _folders[folder.Id] = folder;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        _folders[folder.Id] = folder;
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        foreach (var folderId in folderIds)
        {
            _folders.TryRemove(folderId, out _);
        }

        return Task.CompletedTask;
    }

    private void CollectDescendants(Guid parentFolderId, List<Guid> accumulator)
    {
        foreach (var child in _folders.Values.Where(f => f.ParentFolderId == parentFolderId))
        {
            accumulator.Add(child.Id);
            CollectDescendants(child.Id, accumulator);
        }
    }
}
