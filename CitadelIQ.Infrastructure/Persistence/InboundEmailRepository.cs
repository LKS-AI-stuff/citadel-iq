using CitadelIQ.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

/// <summary><c>InboundEmails</c> — a system table outside the EF model (no workspace, no row-level security).</summary>
public class InboundEmailRepository(CitadelIQDbContext db) : IInboundEmailRepository
{
    private const string Pending = nameof(InboundEmailStatus.Pending);
    private const string Processing = nameof(InboundEmailStatus.Processing);

    private sealed class ClaimRow
    {
        public Guid Id { get; set; }
        public long UidValidity { get; set; }
        public long Uid { get; set; }
        public DateTimeOffset ReceivedAtUtc { get; set; }
        public int Attempts { get; set; }
    }

    public async Task<bool> RegisterAsync(MailboxKey key, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InboundEmails" ("Id", "UidValidity", "Uid", "ReceivedAtUtc", "Status", "Attempts", "NextAttemptAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {(long)key.UidValidity}, {(long)key.Uid}, {receivedAtUtc.ToUniversalTime()}, {Pending}, 0, {now}, {now})
            ON CONFLICT ("UidValidity", "Uid") DO NOTHING
            """, cancellationToken);
        return inserted == 1;
    }

    public Task<int> SupersedeOldValidityAsync(uint currentUidValidity, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InboundEmails"
            SET "Status" = {nameof(InboundEmailStatus.Superseded)}, "LockedUntilUtc" = NULL, "UpdatedAtUtc" = {DateTimeOffset.UtcNow}
            WHERE "UidValidity" <> {(long)currentUidValidity} AND "Status" IN ({Pending}, {Processing})
            """, cancellationToken);

    public async Task<InboundClaim?> ClaimNextAsync(TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Database.SqlQuery<ClaimRow>($"""
            UPDATE "InboundEmails"
            SET "Status" = {Processing}, "LockedUntilUtc" = {now + lease}, "Attempts" = "Attempts" + 1, "UpdatedAtUtc" = {now}
            WHERE "Id" = (
              SELECT "Id" FROM "InboundEmails"
              WHERE ("Status" = {Pending} AND "NextAttemptAtUtc" <= {now})
                 OR ("Status" = {Processing} AND "LockedUntilUtc" < {now})
              ORDER BY "ReceivedAtUtc", "Uid"
              LIMIT 1
              FOR UPDATE SKIP LOCKED)
            RETURNING "Id", "UidValidity", "Uid", "ReceivedAtUtc", "Attempts"
            """).ToListAsync(cancellationToken);

        return rows is [var row]
            ? new InboundClaim(row.Id, new MailboxKey((uint)row.UidValidity, (uint)row.Uid), row.ReceivedAtUtc, row.Attempts)
            : null;
    }

    public async Task<bool> CompleteAsync(Guid id, Guid workspaceId, string? messageIdHash, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InboundEmails"
            SET "Status" = {nameof(InboundEmailStatus.Done)}, "WorkspaceId" = {workspaceId}, "MessageIdHash" = {SqlParameters.Text(messageIdHash)},
                "LockedUntilUtc" = NULL, "LastErrorType" = NULL, "UpdatedAtUtc" = {DateTimeOffset.UtcNow}
            WHERE "Id" = {id} AND "Status" = {Processing}
            """, cancellationToken) == 1;

    public async Task<bool> IgnoreAsync(Guid id, InboundIgnoreReason reason, string? senderDomain, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InboundEmails"
            SET "Status" = {nameof(InboundEmailStatus.Ignored)}, "IgnoreReason" = {reason.ToString()},
                "SenderDomain" = {SqlParameters.Text(SqlParameters.Truncate(senderDomain, 255))},
                "LockedUntilUtc" = NULL, "UpdatedAtUtc" = {DateTimeOffset.UtcNow}
            WHERE "Id" = {id} AND "Status" = {Processing}
            """, cancellationToken) == 1;

    public async Task<bool> ScheduleRetryAsync(Guid id, DateTimeOffset nextAttemptAtUtc, string errorType, bool countAttempt = true, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InboundEmails"
            SET "Status" = {Pending}, "NextAttemptAtUtc" = {nextAttemptAtUtc.ToUniversalTime()}, "LockedUntilUtc" = NULL,
                "Attempts" = greatest(0, "Attempts" - {(countAttempt ? 0 : 1)}),
                "LastErrorType" = {SqlParameters.Truncate(errorType, 100)}, "UpdatedAtUtc" = {DateTimeOffset.UtcNow}
            WHERE "Id" = {id} AND "Status" = {Processing}
            """, cancellationToken) == 1;

    public async Task<bool> FailAsync(Guid id, Guid? workspaceId, string errorType, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InboundEmails"
            SET "Status" = {nameof(InboundEmailStatus.Failed)}, "WorkspaceId" = {SqlParameters.Uuid(workspaceId)}, "LockedUntilUtc" = NULL,
                "LastErrorType" = {SqlParameters.Truncate(errorType, 100)}, "UpdatedAtUtc" = {DateTimeOffset.UtcNow}
            WHERE "Id" = {id} AND "Status" = {Processing}
            """, cancellationToken) == 1;

    public async Task<int> CountProcessedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM "InboundEmails" WHERE "WorkspaceId" IS NOT NULL AND "ReceivedAtUtc" >= {sinceUtc.ToUniversalTime()}
            """).ToListAsync(cancellationToken)).Single();

    public async Task<bool> ExistsFinishedWithMessageIdAsync(string messageIdHash, CancellationToken cancellationToken = default) =>
        (await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM "InboundEmails" WHERE "MessageIdHash" = {messageIdHash} AND "Status" IN ({nameof(InboundEmailStatus.Done)}, {nameof(InboundEmailStatus.Failed)})) AS "Value"
            """).ToListAsync(cancellationToken)).Single();

    public async Task<IReadOnlyList<uint>> ListFinishedUidsAsync(uint uidValidity, IReadOnlyCollection<uint> uids, CancellationToken cancellationToken = default)
    {
        if (uids.Count == 0)
        {
            return [];
        }

        var rows = await db.Database.SqlQuery<long>($"""
            SELECT "Uid" AS "Value" FROM "InboundEmails"
            WHERE "UidValidity" = {(long)uidValidity} AND "Uid" = ANY({uids.Select(u => (long)u).ToArray()})
              AND "Status" IN ({nameof(InboundEmailStatus.Done)}, {nameof(InboundEmailStatus.Ignored)}, {nameof(InboundEmailStatus.Failed)})
            """).ToListAsync(cancellationToken);
        return rows.Select(u => (uint)u).ToList();
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "InboundEmails" WHERE "ReceivedAtUtc" < {cutoffUtc.ToUniversalTime()} AND "Status" NOT IN ({Pending}, {Processing})
            """, cancellationToken);
}
