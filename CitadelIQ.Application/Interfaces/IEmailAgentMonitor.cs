namespace CitadelIQ.Application.Interfaces;

/// <summary>In-process state of the email agent, for <c>/health/email</c> and the portal's support line. No counts,
/// no addresses.</summary>
public interface IEmailAgentMonitor
{
    /// <summary>A support mailbox is configured (<c>EmailAgent:Mailbox:Host</c>), so the agent can actually run.</summary>
    bool MailboxConfigured { get; }

    /// <summary>This instance holds the leader lock and runs the email loops.</summary>
    bool IsLeader { get; }

    /// <summary>When this instance last became the leader; null while it isn't.</summary>
    DateTimeOffset? LeaderSinceUtc { get; }

    DateTimeOffset? LastPollUtc { get; }

    /// <summary>When the current run of mailbox failures began; null while the mailbox works.</summary>
    DateTimeOffset? MailboxFailingSinceUtc { get; }

    void RecordLeadership(bool isLeader);

    void RecordPoll();

    void RecordMailboxFailure();
}
