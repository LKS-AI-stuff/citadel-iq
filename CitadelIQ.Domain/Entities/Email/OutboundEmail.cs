using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// One email CitadelIQ will send (the outbox). The recipient is a user id — the address is re-read from the user record
/// at send time, never taken from the incoming message. The Message-ID is fixed at creation so a resend after a crash
/// can first be looked up in the Sent folder.
/// </summary>
public class OutboundEmail
{
    public const int MaxSubjectLength = 400;
    public const int MaxCancelReasonLength = 64;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public OutboundEmailKind Kind { get; private set; }
    public Guid ToUserId { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }
    /// <summary>Without angle brackets.</summary>
    public string MessageId { get; private set; }
    public string? InReplyTo { get; private set; }
    /// <summary>Space-separated Message-IDs, without angle brackets.</summary>
    public string? References { get; private set; }
    public Guid? ThreadId { get; private set; }
    public Guid? QuestionId { get; private set; }
    public OutboundEmailStatus Status { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }

    private OutboundEmail(Guid id, Guid workspaceId, OutboundEmailKind kind, Guid toUserId, string subject, string body,
        string messageId, string? inReplyTo, string? references, Guid? threadId, Guid? questionId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Kind = kind;
        ToUserId = toUserId;
        Subject = subject;
        Body = body;
        MessageId = messageId;
        InReplyTo = inReplyTo;
        References = references;
        ThreadId = threadId;
        QuestionId = questionId;
        Status = OutboundEmailStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public static OutboundEmail Create(
        Guid workspaceId,
        OutboundEmailKind kind,
        Guid toUserId,
        string subject,
        string body,
        string messageId,
        string? inReplyTo = null,
        IEnumerable<string>? references = null,
        Guid? threadId = null,
        Guid? questionId = null)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new DomainException("An outbound email needs a Message-ID.");
        }

        var refs = string.Join(' ', (references ?? []).Where(r => !string.IsNullOrWhiteSpace(r)));
        return new OutboundEmail(Guid.NewGuid(), workspaceId, kind, toUserId,
            EmailText.Truncate(subject, MaxSubjectLength), body, messageId,
            EmailText.TruncateOrNull(inReplyTo, EmailQuestion.MaxMessageIdLength),
            EmailText.TruncateOrNull(refs, EmailQuestion.MaxReferencesLength),
            threadId, questionId, DateTimeOffset.UtcNow);
    }

    public bool IsFinished => Status is OutboundEmailStatus.Sent or OutboundEmailStatus.Cancelled or OutboundEmailStatus.Failed;

    public void MarkSending()
    {
        if (IsFinished)
        {
            throw new DomainException("This email was already handled.");
        }

        Status = OutboundEmailStatus.Sending;
    }

    public void MarkSent()
    {
        Status = OutboundEmailStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (IsFinished)
        {
            return;
        }

        Status = OutboundEmailStatus.Cancelled;
        CancelReason = EmailText.Truncate(reason, MaxCancelReasonLength);
    }

    public void MarkFailed()
    {
        if (IsFinished)
        {
            return;
        }

        Status = OutboundEmailStatus.Failed;
    }
}
