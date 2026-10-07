using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CitadelIQ.Infrastructure.Persistence;

public class FolderRepository(CitadelIQDbContext db) : IFolderRepository
{
    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Folders.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Folder?> GetRootAsync(CancellationToken cancellationToken = default) =>
        db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.ParentFolderId == null, cancellationToken);

    public async Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid parentFolderId, CancellationToken cancellationToken = default) =>
        await db.Folders
            .AsNoTracking()
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.Name.ToLower())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        // One query for the (id, parent) edges of the current workspace (query filter), then walk the tree in memory.
        var edges = await db.Folders
            .AsNoTracking()
            .Where(f => f.ParentFolderId != null)
            .Select(f => new { f.Id, ParentId = f.ParentFolderId!.Value })
            .ToListAsync(cancellationToken);

        var childrenByParent = edges.ToLookup(e => e.ParentId, e => e.Id);
        var descendants = new List<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(folderId);

        while (pending.Count > 0)
        {
            foreach (var childId in childrenByParent[pending.Dequeue()])
            {
                descendants.Add(childId);
                pending.Enqueue(childId);
            }
        }

        return descendants;
    }

    public Task<bool> ExistsWithNameAsync(Guid parentFolderId, string name, CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();
        return db.Folders.AnyAsync(f => f.ParentFolderId == parentFolderId && f.Name.ToLower() == lowered, cancellationToken);
    }

    public async Task AddAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        db.Folders.Add(folder);
        await SaveTranslatingNameConflictAsync(folder, cancellationToken);
    }

    public async Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        db.Folders.Update(folder);
        await SaveTranslatingNameConflictAsync(folder, cancellationToken);
    }

    public async Task DeleteManyAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        // ON DELETE CASCADE on the FKs removes any remaining descendants/documents/chunks too.
        await db.Folders.Where(f => folderIds.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The (ParentFolderId, lower(Name)) unique index is the race-proof backstop for the
    /// application's check-then-act name check; surface it as the same friendly validation error.</summary>
    private async Task SaveTranslatingNameConflictAsync(Folder folder, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ValidationException($"A folder named \"{folder.Name}\" already exists here.");
        }
    }
}
