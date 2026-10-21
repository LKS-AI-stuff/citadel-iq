namespace CitadelIQ.Domain.Enums;

/// <summary>Why an email question was ignored, drafted, paused or failed. Null for auto-replies.</summary>
public enum EmailReason
{
    NotAQuestion,
    Thanks,
    AutomatedMessage,
    NotificationReply,
    OverLimit,
    ThreadLimit,
    AdminInThread,
    NoAnswer,
    LowConfidence,
    /// <summary>The answer would have been sent automatically, but the agent is in <c>DraftsOnly</c> mode.</summary>
    DraftsOnly,
    AnswersDisabled,
    Failed
}
