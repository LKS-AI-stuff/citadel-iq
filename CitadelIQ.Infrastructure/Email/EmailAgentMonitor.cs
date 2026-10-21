using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Infrastructure.Email;

public sealed class EmailAgentMonitor(bool mailboxConfigured) : IEmailAgentMonitor
{
    private long _lastPollTicks;
    private long _failingSinceTicks;
    private long _leaderSinceTicks;

    public bool MailboxConfigured { get; } = mailboxConfigured;

    public bool IsLeader => Interlocked.Read(ref _leaderSinceTicks) != 0;

    public DateTimeOffset? LeaderSinceUtc => Read(ref _leaderSinceTicks);

    public DateTimeOffset? LastPollUtc => Read(ref _lastPollTicks);

    public DateTimeOffset? MailboxFailingSinceUtc => Read(ref _failingSinceTicks);

    public void RecordLeadership(bool isLeader)
    {
        if (isLeader)
        {
            Interlocked.CompareExchange(ref _leaderSinceTicks, DateTimeOffset.UtcNow.UtcTicks, 0);
        }
        else
        {
            Interlocked.Exchange(ref _leaderSinceTicks, 0);
        }
    }

    public void RecordPoll()
    {
        Interlocked.Exchange(ref _lastPollTicks, DateTimeOffset.UtcNow.UtcTicks);
        Interlocked.Exchange(ref _failingSinceTicks, 0);
    }

    public void RecordMailboxFailure() =>
        Interlocked.CompareExchange(ref _failingSinceTicks, DateTimeOffset.UtcNow.UtcTicks, 0);

    private static DateTimeOffset? Read(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTimeOffset(value, TimeSpan.Zero);
    }
}
