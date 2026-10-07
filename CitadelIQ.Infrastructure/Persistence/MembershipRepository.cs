using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class MembershipRepository(CitadelIQDbContext db) : IMembershipRepository
{
    public Task<Membership?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Memberships.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<MemberRecord>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var rows = await (from membership in db.Memberships.AsNoTracking()
                          join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                          where membership.WorkspaceId == workspaceId
                          orderby user.DisplayName
                          select new { membership, user })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new MemberRecord(r.membership, r.user)).ToList();
    }

    public async Task AddAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        db.Memberships.Add(membership);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("PK_Memberships"))
        {
            db.Entry(membership).State = EntityState.Detached;
            throw new ValidationException("You already belong to a workspace.");
        }
    }

    public async Task RunLockedAsync(Guid workspaceId, Func<WorkspaceMembershipSet, Task> change, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Serializes every membership change in this workspace: a second transaction waits here and then sees the
        // first one's committed result (READ COMMITTED re-reads after the lock is granted).
        var workspace = await db.Workspaces
            .FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {workspaceId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Organization not found.");

        var memberships = await db.Memberships.Where(m => m.WorkspaceId == workspaceId).ToListAsync(cancellationToken);
        var pending = await db.JoinRequests
            .Where(r => r.WorkspaceId == workspaceId && r.Status == JoinRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        var set = new WorkspaceMembershipSet(workspace, memberships, pending);
        await change(set);

        db.Memberships.AddRange(set.Added);
        db.Memberships.RemoveRange(set.Removed.Where(m => !set.Added.Contains(m)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("PK_Memberships"))
        {
            throw new ValidationException("That user already belongs to a workspace.");
        }

        // Once the change is applied, a client disconnect must not leave it half-committed.
        await transaction.CommitAsync(CancellationToken.None);
    }
}
