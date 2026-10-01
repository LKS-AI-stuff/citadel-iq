using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class DocumentRepository(CitadelIQDbContext db) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> GetByFolderIdAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        await db.Documents
            .AsNoTracking()
            .Where(d => d.FolderId == folderId)
            .OrderBy(d => d.FileName.ToLower())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> GetByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken cancellationToken = default) =>
        await db.Documents
            .AsNoTracking()
            .Where(d => folderIds.Contains(d.FolderId))
            .OrderBy(d => d.FileName.ToLower())
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        db.Documents.Update(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await db.Documents.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
