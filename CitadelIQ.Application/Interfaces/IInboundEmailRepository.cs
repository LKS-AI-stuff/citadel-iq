namespace CitadelIQ.Application.Interfaces;

public enum InboundEmailStatus
{
    Pending,
    Processing,
    Done,
    /// <summary>Operator log only: unauthenticated, unknown/ambiguous sender, our own mail, missing, duplicate.</summary>
    Ignored,
    Failed,
    /// <summary>Left unfinished when the inbox's UIDVALIDITY changed; the message is re-registered under its new UID.</summary>
    Superseded
}

public enum InboundIgnoreReason
{
    Unauthenticated,
    UnknownSender,
    AmbiguousSender,
    Loop,
    Missing,
    Duplicate
}

/// <summary>A claimed processing row. <see cref="Attempt"/> counts this claim (1 = first try).</summary>
public record InboundClaim(Guid Id, MailboxKey Key, DateTimeOffset ReceivedAtUtc, int Attempt);

/// <summary>
/// <c>InboundEmails</c>: a system table (no workspace, no row-level security, no email content) that is both the
/// processing state and the operator-only log of untrusted mail. Usable from any scope.
/// </summary>
public interface IInboundEmailRepository
{
    /// <summary>Inserts a Pending row unless the key is already known. True when inserted.</summary>
    Task<bool> RegisterAsync(MailboxKey key, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Marks unfinished rows from any other UIDVALIDITY as Superseded.</summary>
    Task<int> SupersedeOldValidityAsync(uint currentUidValidity, CancellationToken cancellationToken = default);

    /// <summary>Claims the oldest due row (Pending and due, or Processing with an expired lease) with
    /// <c>FOR UPDATE SKIP LOCKED</c>, marks it Processing until now + <paramref name="lease"/> and counts the attempt.</summary>
    Task<InboundClaim?> ClaimNextAsync(TimeSpan lease, CancellationToken cancellationToken = default);

    // The four writes below finish a claim. They apply only while the row is still Processing and return false
    // otherwise: a UIDVALIDITY change superseded it mid-flight, and the message is handled under its new key instead.

    Task<bool> CompleteAsync(Guid id, Guid workspaceId, string? messageIdHash, CancellationToken cancellationToken = default);

    Task<bool> IgnoreAsync(Guid id, InboundIgnoreReason reason, string? senderDomain, CancellationToken cancellationToken = default);

    /// <summary>Back to Pending until <paramref name="nextAttemptAtUtc"/>. With <paramref name="countAttempt"/> false the
    /// claim is not counted (the mailbox was unreachable: processing pauses rather than failing the email).</summary>
    Task<bool> ScheduleRetryAsync(Guid id, DateTimeOffset nextAttemptAtUtc, string errorType, bool countAttempt = true, CancellationToken cancellationToken = default);

    Task<bool> FailAsync(Guid id, Guid? workspaceId, string errorType, CancellationToken cancellationToken = default);

    /// <summary>Emails from resolved senders received since <paramref name="sinceUtc"/> (the total processing cap).</summary>
    Task<int> CountProcessedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>A finished row already carries this DKIM-signed Message-ID hash (duplicate after a UIDVALIDITY change).</summary>
    Task<bool> ExistsFinishedWithMessageIdAsync(string messageIdHash, CancellationToken cancellationToken = default);

    /// <summary>Of these UIDs, the ones already finished (Done, Ignored, Failed) — still in the inbox only because the
    /// archive move failed; the listener moves them again.</summary>
    Task<IReadOnlyList<uint>> ListFinishedUidsAsync(uint uidValidity, IReadOnlyCollection<uint> uids, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}
