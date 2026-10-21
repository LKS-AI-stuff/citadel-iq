using CitadelIQ.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary><c>AdminNotificationSchedule</c> — a system table outside the EF model; one row per admin.</summary>
public class AdminNotificationSchedule(CitadelIQDbContext db) : IAdminNotificationSchedule
{
    private sealed class EntryRow
    {
        public Guid UserId { get; set; }
        public Guid WorkspaceId { get; set; }
        public DateTimeOffset? LastSentAtUtc { get; set; }
    }

    public Task ScheduleAsync(Guid userId, Guid workspaceId, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        // Keep an already-pending due time; otherwise due at max(now, last sent + interval). ON CONFLICT DO UPDATE waits
        // for a worker that holds the row (LockDueAsync), so a draft created mid-send is never missed.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AdminNotificationSchedule" AS s ("UserId", "WorkspaceId", "DueAtUtc", "LastSentAtUtc")
            VALUES ({userId}, {workspaceId}, {now}, NULL)
            ON CONFLICT ("UserId") DO UPDATE SET
              "WorkspaceId" = EXCLUDED."WorkspaceId",
              "DueAtUtc" = COALESCE(s."DueAtUtc", GREATEST(EXCLUDED."DueAtUtc", COALESCE(s."LastSentAtUtc" + {interval}, EXCLUDED."DueAtUtc")))
            """, cancellationToken);
    }

    public async Task<IReadOnlyList<DueNotification>> ListDueAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQuery<EntryRow>($"""
            SELECT "UserId", "WorkspaceId", "LastSentAtUtc" FROM "AdminNotificationSchedule"
            WHERE "DueAtUtc" <= {nowUtc.ToUniversalTime()} ORDER BY "DueAtUtc" LIMIT {limit}
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new DueNotification(r.UserId, r.WorkspaceId)).ToList();
    }

    public async Task<NotificationScheduleEntry?> LockDueAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQuery<EntryRow>($"""
            SELECT "UserId", "WorkspaceId", "LastSentAtUtc" FROM "AdminNotificationSchedule"
            WHERE "UserId" = {userId} AND "DueAtUtc" <= {nowUtc.ToUniversalTime()}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        return rows is [var row] ? new NotificationScheduleEntry(row.UserId, row.WorkspaceId, row.LastSentAtUtc) : null;
    }

    public Task MarkHandledAsync(Guid userId, DateTimeOffset? sentAtUtc, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AdminNotificationSchedule"
            SET "DueAtUtc" = NULL, "LastSentAtUtc" = COALESCE({SqlParameters.Timestamp(sentAtUtc)}, "LastSentAtUtc")
            WHERE "UserId" = {userId}
            """, cancellationToken);
}
