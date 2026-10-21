namespace CitadelIQ.Domain.Enums;

/// <summary>What the email agent finally did with one email question (set once).</summary>
public enum EmailOutcome
{
    Ignored,
    AutoReplied,
    Drafted,
    /// <summary>Individual workspaces: a "no confident answer" reply was sent instead of a draft.</summary>
    NoConfidentAnswer,
    /// <summary>Individual workspaces while the agent is in <c>DraftsOnly</c> (or answers are off): nothing sent.</summary>
    Paused,
    Failed
}
