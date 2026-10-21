using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Entities;

/// <summary>
/// One email from an authenticated, registered sender and what the agent did with it — the row behind the review
/// queue and the email activity list. Created in memory, given exactly one outcome, then saved once.
/// </summary>
public class EmailQuestion
{
    public const int MaxSubjectLength = 300;
    public const int MaxMessageIdLength = 998;
    public const int MaxReferencesLength = 4000;

    private bool _decided;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid? ThreadId { get; private set; }
    /// <summary>The system processing row; cleared when that row is removed by retention.</summary>
    public Guid? InboundEmailId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public string Subject { get; private set; }
    /// <summary>The standalone question the triage step extracted (null when the email was not a question).</summary>
    public string? QuestionText { get; private set; }
    public EmailOutcome Outcome { get; private set; }
    public EmailReason? Reason { get; private set; }
    /// <summary>The judge's "fully answered" score (0–1), when an answer was judged.</summary>
    public double? Confidence { get; private set; }
    /// <summary>Whether the confidence gate passed — recorded even when the mode or workspace kind stopped the auto-reply,
    /// so DraftsOnly can be compared with what would have been sent.</summary>
    public bool? WouldAutoReply { get; private set; }
    /// <summary>The text actually sent automatically (auto-reply or labelled suggestion), or the draft's suggestion.</summary>
    public string? AnswerText { get; private set; }
    public IReadOnlyList<EmailSourceRef> Sources { get; private set; } = [];
    public string? InboundMessageId { get; private set; }
    /// <summary>Space-separated <c>References</c> of the inbound email, for threading later replies.</summary>
    public string? InboundReferences { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    private EmailQuestion(
        Guid id, Guid workspaceId, Guid? threadId, Guid? inboundEmailId, Guid senderUserId, DateTimeOffset receivedAtUtc,
        string subject, string? inboundMessageId, string? inboundReferences, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ThreadId = threadId;
        InboundEmailId = inboundEmailId;
        SenderUserId = senderUserId;
        ReceivedAtUtc = receivedAtUtc;
        Subject = subject;
        InboundMessageId = inboundMessageId;
        InboundReferences = inboundReferences;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static EmailQuestion Create(
        Guid workspaceId,
        Guid inboundEmailId,
        Guid senderUserId,
        DateTimeOffset receivedAtUtc,
        string? subject,
        string? inboundMessageId,
        IEnumerable<string> inboundReferences,
        TimeSpan retention)
    {
        var references = string.Join(' ', inboundReferences.Where(r => !string.IsNullOrWhiteSpace(r)));
        return new EmailQuestion(
            Guid.NewGuid(), workspaceId, null, inboundEmailId, senderUserId, receivedAtUtc,
            EmailText.Truncate(subject, MaxSubjectLength),
            EmailText.TruncateOrNull(inboundMessageId, MaxMessageIdLength),
            EmailText.TruncateOrNull(references, MaxReferencesLength),
            receivedAtUtc + retention);
    }

    public bool IsDecided => _decided;

    public void AttachToThread(EmailThread thread)
    {
        if (thread.WorkspaceId != WorkspaceId)
        {
            throw new DomainException("A question can only join a thread in its own workspace.");
        }

        ThreadId = thread.Id;
    }

    public void SetQuestion(string question) => QuestionText = question.Trim();

    public void RecordGate(bool wouldAutoReply, double? confidence)
    {
        WouldAutoReply = wouldAutoReply;
        Confidence = confidence;
    }

    public void Ignore(EmailReason reason) => Decide(EmailOutcome.Ignored, reason);

    public void AutoReply(string answer, IReadOnlyList<EmailSourceRef> sources)
    {
        Decide(EmailOutcome.AutoReplied, null);
        AnswerText = answer;
        Sources = sources;
    }

    public void Draft(EmailReason reason, string? suggestion, IReadOnlyList<EmailSourceRef> sources)
    {
        Decide(EmailOutcome.Drafted, reason);
        AnswerText = suggestion;
        Sources = sources;
    }

    /// <summary>Individual workspaces: the "no confident answer" reply, optionally with a labelled suggestion.</summary>
    public void NoConfidentAnswer(EmailReason reason, string? suggestion, IReadOnlyList<EmailSourceRef> sources)
    {
        Decide(EmailOutcome.NoConfidentAnswer, reason);
        AnswerText = suggestion;
        Sources = sources;
    }

    public void Pause(EmailReason reason) => Decide(EmailOutcome.Paused, reason);

    public void Fail() => Decide(EmailOutcome.Failed, EmailReason.Failed);

    private void Decide(EmailOutcome outcome, EmailReason? reason)
    {
        if (_decided)
        {
            throw new DomainException("This email already has an outcome.");
        }

        _decided = true;
        Outcome = outcome;
        Reason = reason;
    }
}
