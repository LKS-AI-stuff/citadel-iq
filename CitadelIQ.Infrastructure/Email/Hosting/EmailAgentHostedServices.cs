using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>Shared loop shape: runs only on the leader and while <c>EmailAgent:Mode</c> allows (read every iteration);
/// every iteration catches and logs (exception type only) so one bad email never stops the loop.</summary>
public abstract class EmailAgentLoop(EmailAgentLeader leader, IOptionsMonitor<EmailAgentOptions> options, ILogger logger) : BackgroundService
{
    private static readonly TimeSpan Paused = TimeSpan.FromSeconds(5);

    protected IOptionsMonitor<EmailAgentOptions> Options => options;

    protected virtual bool RunsWhenOff => false;

    /// <summary>One iteration; returns how long to wait before the next.</summary>
    protected abstract Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = Paused;
                if (leader.IsLeader && (RunsWhenOff || options.CurrentValue.Mode != EmailAgentMode.Off))
                {
                    try
                    {
                        delay = await RunOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError("{Loop} iteration failed ({ExceptionType})", GetType().Name, ex.GetType().Name);
                    }
                }

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

/// <summary>IDLE (or poll) → register new mail. Backs off 1 s → 5 min while the mailbox fails, and resumes where it
/// stopped: unprocessed mail never left the inbox.</summary>
public sealed class MailboxListenerService(
    EmailAgentLeader leader,
    MailboxSynchronizer synchronizer,
    IMailbox mailbox,
    IEmailAgentMonitor monitor,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<MailboxListenerService> logger) : EmailAgentLoop(leader, options, logger)
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);
    private TimeSpan _backoff = TimeSpan.FromSeconds(1);

    protected override async Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await synchronizer.SyncAsync(stoppingToken);
            _backoff = TimeSpan.FromSeconds(1);
            await mailbox.WaitForChangeAsync(TimeSpan.FromSeconds(Options.CurrentValue.PollIntervalSeconds), stoppingToken);
            return TimeSpan.Zero;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            monitor.RecordMailboxFailure();
            logger.LogWarning("Support mailbox unavailable ({ExceptionType}); retrying in {Seconds} s", ex.GetType().Name, (int)_backoff.TotalSeconds);
            var wait = _backoff;
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
            return wait;
        }
    }
}

/// <summary>Claims inbound emails one at a time (concurrency 1 keeps them in arrival order). Pauses while the mailbox
/// is failing, so mail waits rather than failing.</summary>
public sealed class InboundProcessorService(
    EmailAgentLeader leader,
    InboundEmailPipeline pipeline,
    IEmailAgentMonitor monitor,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<InboundProcessorService> logger) : EmailAgentLoop(leader, options, logger)
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(3);

    protected override async Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken)
    {
        if (monitor.MailboxFailingSinceUtc is not null)
        {
            return Idle;
        }

        return await pipeline.ProcessNextAsync(stoppingToken) ? TimeSpan.Zero : Idle;
    }
}

public sealed class OutboundSenderService(
    EmailAgentLeader leader,
    OutboundEmailPipeline pipeline,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<OutboundSenderService> logger) : EmailAgentLoop(leader, options, logger)
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(3);

    protected override async Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken) =>
        await pipeline.SendNextAsync(stoppingToken) ? TimeSpan.Zero : Idle;
}

/// <summary>Every minute: due draft notifications. Every <c>MaintenanceIntervalMinutes</c>: draft expiry and retention.
/// Retention of stored data keeps running while the agent is off (privacy); notifications and the mailbox purge don't.</summary>
public sealed class EmailMaintenanceService(
    EmailAgentLeader leader,
    EmailMaintenanceRunner runner,
    IEmailAgentMonitor monitor,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<EmailMaintenanceService> logger) : EmailAgentLoop(leader, options, logger)
{
    private DateTimeOffset _lastRetention = DateTimeOffset.MinValue;

    protected override bool RunsWhenOff => true;

    protected override async Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken)
    {
        var o = Options.CurrentValue;
        var on = o.Mode != EmailAgentMode.Off;

        if (on)
        {
            await runner.SendDueNotificationsAsync(stoppingToken);
        }

        if (DateTimeOffset.UtcNow - _lastRetention >= TimeSpan.FromMinutes(o.MaintenanceIntervalMinutes))
        {
            _lastRetention = DateTimeOffset.UtcNow;
            await runner.RunRetentionAsync(purgeArchive: on && monitor.MailboxFailingSinceUtc is null, stoppingToken);
        }

        return TimeSpan.FromMinutes(1);
    }
}
