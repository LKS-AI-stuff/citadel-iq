using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class WorkspaceRepository(CitadelIQDbContext db) : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<Workspace?> GetByJoinCodeAsync(string normalizedJoinCode, CancellationToken cancellationToken = default) =>
        db.Workspaces.AsNoTracking().FirstOrDefaultAsync(w => w.JoinCode == normalizedJoinCode, cancellationToken);

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        // Tracked ⇒ only changed columns are written (see UserAccountRepository.UpdateAsync).
        if (db.Entry(workspace).State == EntityState.Detached)
        {
            db.Workspaces.Update(workspace);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
