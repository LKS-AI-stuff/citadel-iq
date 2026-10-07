using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

public class UserAccountRepository(CitadelIQDbContext db) : IUserAccountRepository
{
    public Task<UserAccount?> GetByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Issuer == issuer && u.Subject == subject, cancellationToken);

    public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        await (from user in db.Users.AsNoTracking()
               where userIds.Contains(user.Id)
               join membership in db.Memberships on user.Id equals membership.UserId into memberships
               from membership in memberships.DefaultIfEmpty()
               select new UserDisplayInfo(user.Id, user.DisplayName, user.ClosedAtUtc != null, membership != null ? membership.WorkspaceId : null))
            .ToListAsync(cancellationToken);

    public async Task<UserAccount> AddOrGetAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("UX_Users_Issuer_Subject"))
        {
            // A concurrent first request for the same identity won the insert.
            db.Entry(user).State = EntityState.Detached;
            return await GetByIdentityAsync(user.Issuer, user.Subject, cancellationToken)
                ?? throw new InvalidOperationException("User vanished after a unique-key conflict.");
        }
    }

    public async Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        // Only attach when detached: Update() on a tracked entity marks every column modified, so a sign-in racing an
        // account closure would write ClosedAtUtc back to NULL. Tracked ⇒ EF writes just the columns that changed.
        if (db.Entry(user).State == EntityState.Detached)
        {
            db.Users.Update(user);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
