using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>A proposed reply waiting in an organization's review queue. Never stored in a mailbox.</summary>
public class EmailDraft
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid QuestionId { get; private set; }
    public EmailDraftStatus Status { get; private set; }
    public EmailReason Reason { get; private set; }
    public string? SuggestedText { get; private set; }
    /// <summary>The judge's note on what the suggestion leaves out — a hint for reviewers, never logged.</summary>
    public string? Missing { get; private set; }
    public string? SentText { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    private EmailDraft(Guid id, Guid workspaceId, Guid questionId, EmailReason reason, string? suggestedText, string? missing,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        QuestionId = questionId;
        Status = EmailDraftStatus.Pending;
        Reason = reason;
        SuggestedText = suggestedText;
        Missing = missing;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static EmailDraft Create(EmailQuestion question, EmailReason reason, string? suggestedText, string? missing, TimeSpan expiry)
    {
        var now = DateTimeOffset.UtcNow;
        return new EmailDraft(Guid.NewGuid(), question.WorkspaceId, question.Id, reason,
            string.IsNullOrWhiteSpace(suggestedText) ? null : suggestedText,
            string.IsNullOrWhiteSpace(missing) ? null : missing.Trim(),
            now, now + expiry);
    }

    public void Send(Guid userId, string text)
    {
        RequirePending();
        Status = EmailDraftStatus.Sent;
        SentText = text;
        Decided(userId);
    }

    public void Dismiss(Guid userId)
    {
        RequirePending();
        Status = EmailDraftStatus.Dismissed;
        Decided(userId);
    }

    /// <summary>Retention: the suggestion is cleared along with the status change.</summary>
    public void Expire()
    {
        RequirePending();
        Status = EmailDraftStatus.Expired;
        SuggestedText = null;
        Missing = null;
        DecidedAtUtc = DateTimeOffset.UtcNow;
    }

    private void Decided(Guid userId)
    {
        DecidedByUserId = userId;
        DecidedAtUtc = DateTimeOffset.UtcNow;
    }

    private void RequirePending()
    {
        if (Status != EmailDraftStatus.Pending)
        {
            throw new DomainException("This draft was already sent or dismissed.");
        }
    }
}
