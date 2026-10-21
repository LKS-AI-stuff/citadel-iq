using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Interfaces;

// Workspace tables: every query is confined to the scope's workspace by query filters and row-level security.

public interface IEmailThreadRepository
{
    /// <summary>Tracked: changes are saved by <see cref="UpdateAsync"/>.</summary>
    Task<EmailThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(EmailThread thread, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmailThread thread, CancellationToken cancellationToken = default);

    /// <summary>Retention: threads with no activity since <paramref name="cutoffUtc"/> (their questions and outbound
    /// mail cascade with them).</summary>
    Task<int> DeleteInactiveAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}

/// <summary>A question in a thread plus its draft, if any (for building follow-up history).</summary>
public record EmailThreadEntry(EmailQuestion Question, EmailDraft? Draft);

/// <summary>An activity row: the question, the sender's display name and the delivery state of its reply (if one).</summary>
public record EmailActivityRecord(EmailQuestion Question, string SenderDisplayName, OutboundEmailStatus? Delivery);

public interface IEmailQuestionRepository
{
    Task AddAsync(EmailQuestion question, CancellationToken cancellationToken = default);

    Task<EmailQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Oldest first.</summary>
    Task<IReadOnlyList<EmailThreadEntry>> ListThreadAsync(Guid threadId, CancellationToken cancellationToken = default);

    /// <summary>Questions received since <paramref name="sinceUtc"/> — from one sender, or the whole workspace when null.</summary>
    Task<int> CountSinceAsync(Guid? senderUserId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Newest first (ties by id), paged by arrival time: rows received at or before <paramref name="beforeUtc"/>,
    /// skipping the <paramref name="skipAtBefore"/> rows received exactly at it that an earlier page already returned (so
    /// rows sharing a timestamp at a page boundary are never lost). One sender's rows when given.</summary>
    Task<IReadOnlyList<EmailActivityRecord>> ListActivityAsync(DateTimeOffset? beforeUtc, int skipAtBefore, int limit, Guid? senderUserId, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>A draft with its question and the asker's display name.</summary>
public record EmailDraftRecord(EmailDraft Draft, EmailQuestion Question, string AskerDisplayName);

public interface IEmailDraftRepository
{
    Task AddAsync(EmailDraft draft, CancellationToken cancellationToken = default);

    Task<EmailDraftRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<EmailDraftRecord>> ListAsync(EmailDraftStatus status, int limit, CancellationToken cancellationToken = default);

    /// <summary>Pending drafts, optionally only those created after <paramref name="createdAfterUtc"/>.</summary>
    Task<int> CountPendingAsync(DateTimeOffset? createdAfterUtc = null, CancellationToken cancellationToken = default);

    /// <summary>Oldest first.</summary>
    Task<IReadOnlyList<EmailDraftRecord>> ListPendingAsync(DateTimeOffset? createdAfterUtc, int limit, CancellationToken cancellationToken = default);

    /// <summary>Persists a Sent/Dismissed decision only if the draft is still Pending in the database (the race
    /// between two reviewers). False when someone else decided it first.</summary>
    Task<bool> TrySaveDecisionAsync(EmailDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Pending drafts past their expiry become Expired, with the suggestion cleared.</summary>
    Task<int> ExpireAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public interface IOutboundEmailRepository
{
    Task AddAsync(OutboundEmail email, CancellationToken cancellationToken = default);

    /// <summary>Tracked: changes are saved by <see cref="UpdateAsync"/>.</summary>
    Task<OutboundEmail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(OutboundEmail email, CancellationToken cancellationToken = default);

    /// <summary>Our own messages with these Message-IDs (for matching In-Reply-To/References to a thread).</summary>
    Task<IReadOnlyList<OutboundEmail>> FindByMessageIdsAsync(IReadOnlyCollection<string> messageIds, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}

/// <summary>A claimed send. <see cref="Attempt"/> counts this claim (1 = first try).</summary>
public record OutboundClaim(Guid OutboundEmailId, Guid WorkspaceId, int Attempt);

/// <summary><c>OutboundEmailQueue</c>: system table telling the sender which workspace to enter and which outbound row
/// to load. Holds no content.</summary>
public interface IOutboundEmailQueue
{
    Task EnqueueAsync(Guid workspaceId, Guid outboundEmailId, CancellationToken cancellationToken = default);

    Task<OutboundClaim?> ClaimNextAsync(TimeSpan lease, CancellationToken cancellationToken = default);

    /// <summary>Finished (sent, cancelled or failed). <paramref name="sentAtUtc"/> counts toward the daily send cap.</summary>
    Task MarkDoneAsync(Guid outboundEmailId, DateTimeOffset? sentAtUtc, CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(Guid outboundEmailId, DateTimeOffset nextAttemptAtUtc, CancellationToken cancellationToken = default);

    Task<int> CountSentSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    Task<int> DeleteFinishedOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}

public record DueNotification(Guid UserId, Guid WorkspaceId);

public record NotificationScheduleEntry(Guid UserId, Guid WorkspaceId, DateTimeOffset? LastSentAtUtc);

/// <summary><c>AdminNotificationSchedule</c>: system table, at most one pending draft notification per admin.</summary>
public interface IAdminNotificationSchedule
{
    /// <summary>Upsert: due at max(now, last sent + <paramref name="interval"/>), keeping an earlier due time.</summary>
    Task ScheduleAsync(Guid userId, Guid workspaceId, TimeSpan interval, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DueNotification>> ListDueAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default);

    /// <summary>Row-locks the admin's entry in the current transaction if it is due; null when not due or already
    /// being handled by another worker.</summary>
    Task<NotificationScheduleEntry?> LockDueAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Clears the due time; records <paramref name="sentAtUtc"/> as the last notification when one was sent.</summary>
    Task MarkHandledAsync(Guid userId, DateTimeOffset? sentAtUtc, CancellationToken cancellationToken = default);
}
