namespace CitadelIQ.Application.Interfaces;

/// <summary>A message's position in the support mailbox: IMAP UIDVALIDITY + UID. The processing key — never the
/// sender-controlled Message-ID.</summary>
public record MailboxKey(uint UidValidity, uint Uid);

public record MailboxEntry(MailboxKey Key, DateTimeOffset ReceivedAtUtc);

/// <summary>The inbox's current UIDVALIDITY (known even when the inbox is empty) and its messages.</summary>
public record MailboxListing(uint UidValidity, IReadOnlyList<MailboxEntry> Entries);

/// <summary>One result from the mailbox provider's own <c>Authentication-Results</c> header, e.g. ("spf", "pass",
/// domain of smtp.mailfrom) or ("dkim", "pass", header.d / domain of header.i).</summary>
public record AuthResult(string Method, string Result, string? Domain);

/// <summary>Header signals that mark a message as automatically generated (RFC 3834 and common conventions).
/// <see cref="EmptyReturnPath"/>: a <c>Return-Path: &lt;&gt;</c> (null sender — bounces).</summary>
public record AutomationSignals(
    string? AutoSubmitted,
    string? Precedence,
    bool HasListHeaders,
    bool EmptyReturnPath,
    bool IsDeliveryReport,
    bool HasAutoReplyHeader)
{
    public static readonly AutomationSignals None = new(null, null, false, false, false, false);
}

/// <summary>A parsed incoming email. Message-IDs are without angle brackets.</summary>
public record InboundEmail(
    MailboxKey Key,
    DateTimeOffset ReceivedAtUtc,
    string? MessageId,
    bool MessageIdSigned,
    string? InReplyTo,
    IReadOnlyList<string> References,
    string FromAddress,
    string Subject,
    /// <summary>text/plain, or HTML converted to text.</summary>
    string TextBody,
    /// <summary>From the provider's own <c>Authentication-Results</c> header only (trusted authserv-id).</summary>
    IReadOnlyList<AuthResult> TrustedAuthResults,
    AutomationSignals Automation);

/// <summary>
/// The support mailbox (receive side), with IMAP semantics but provider-neutral types. Implementations throw
/// <see cref="Common.MailboxUnavailableException"/> for connection/protocol failures.
/// </summary>
public interface IMailbox
{
    Task<MailboxListing> ListInboxAsync(CancellationToken cancellationToken = default);

    /// <summary>Null when the message is no longer in the inbox (or the inbox's UIDVALIDITY changed).</summary>
    Task<InboundEmail?> FetchAsync(MailboxKey key, CancellationToken cancellationToken = default);

    /// <summary>Moves the message to the archive folder. Idempotent: a message that is already gone is success.</summary>
    Task ArchiveAsync(MailboxKey key, CancellationToken cancellationToken = default);

    Task<bool> SentFolderContainsAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>Returns when the inbox may have changed (IMAP IDLE where supported) or after <paramref name="maxWait"/>.</summary>
    Task WaitForChangeAsync(TimeSpan maxWait, CancellationToken cancellationToken = default);

    /// <summary>Deletes archived messages received before <paramref name="olderThan"/>.</summary>
    Task PurgeArchiveAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
