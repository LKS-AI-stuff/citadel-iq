using CitadelIQ.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary><c>OutboundEmailQueue</c> — a system table outside the EF model: which workspace to enter and which
/// outbound row to send. No content.</summary>
public class OutboundEmailQueue(CitadelIQDbContext db) : IOutboundEmailQueue
{
    private const string Pending = "Pending";
    private const string Processing = "Processing";
    private const string Done = "Done";

    private sealed class ClaimRow
    {
        public Guid OutboundEmailId { get; set; }
        public Guid WorkspaceId { get; set; }
        public int Attempts { get; set; }
    }

    public Task EnqueueAsync(Guid workspaceId, Guid outboundEmailId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OutboundEmailQueue" ("OutboundEmailId", "WorkspaceId", "Status", "Attempts", "NextAttemptAtUtc", "CreatedAtUtc")
            VALUES ({outboundEmailId}, {workspaceId}, {Pending}, 0, {now}, {now})
            """, cancellationToken);
    }

    public async Task<OutboundClaim?> ClaimNextAsync(TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Database.SqlQuery<ClaimRow>($"""
            UPDATE "OutboundEmailQueue"
            SET "Status" = {Processing}, "LockedUntilUtc" = {now + lease}, "Attempts" = "Attempts" + 1
            WHERE "OutboundEmailId" = (
              SELECT "OutboundEmailId" FROM "OutboundEmailQueue"
              WHERE ("Status" = {Pending} AND "NextAttemptAtUtc" <= {now})
                 OR ("Status" = {Processing} AND "LockedUntilUtc" < {now})
              ORDER BY "NextAttemptAtUtc"
              LIMIT 1
              FOR UPDATE SKIP LOCKED)
            RETURNING "OutboundEmailId", "WorkspaceId", "Attempts"
            """).ToListAsync(cancellationToken);

        return rows is [var row] ? new OutboundClaim(row.OutboundEmailId, row.WorkspaceId, row.Attempts) : null;
    }

    public Task MarkDoneAsync(Guid outboundEmailId, DateTimeOffset? sentAtUtc, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "OutboundEmailQueue" SET "Status" = {Done}, "LockedUntilUtc" = NULL, "SentAtUtc" = {SqlParameters.Timestamp(sentAtUtc)}
            WHERE "OutboundEmailId" = {outboundEmailId}
            """, cancellationToken);

    public Task ScheduleRetryAsync(Guid outboundEmailId, DateTimeOffset nextAttemptAtUtc, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "OutboundEmailQueue" SET "Status" = {Pending}, "LockedUntilUtc" = NULL, "NextAttemptAtUtc" = {nextAttemptAtUtc.ToUniversalTime()}
            WHERE "OutboundEmailId" = {outboundEmailId}
            """, cancellationToken);

    public async Task<int> CountSentSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM "OutboundEmailQueue" WHERE "SentAtUtc" >= {sinceUtc.ToUniversalTime()}
            """).ToListAsync(cancellationToken)).Single();

    public Task<int> DeleteFinishedOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "OutboundEmailQueue" WHERE "Status" = {Done} AND "CreatedAtUtc" < {cutoffUtc.ToUniversalTime()}
            """, cancellationToken);
}
