using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class JoinRequestRepository(CitadelIQDbContext db) : IJoinRequestRepository
{
    public Task<JoinRequest?> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.JoinRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == JoinRequestStatus.Pending, cancellationToken);

    public Task<JoinRequest?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.JoinRequests.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<JoinRequestRecord>> ListPendingAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var rows = await (from request in db.JoinRequests.AsNoTracking()
                          join user in db.Users.AsNoTracking() on request.UserId equals user.Id
                          where request.WorkspaceId == workspaceId && request.Status == JoinRequestStatus.Pending
                          orderby request.CreatedAtUtc
                          select new { request, user })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new JoinRequestRecord(r.request, r.user)).ToList();
    }

    public async Task AddAsync(JoinRequest request, CancellationToken cancellationToken = default)
    {
        db.JoinRequests.Add(request);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("UX_JoinRequests_PendingPerUser"))
        {
            db.Entry(request).State = EntityState.Detached;
            throw new ValidationException("You already have a pending request to join an organization. Cancel it first.");
        }
    }
}
