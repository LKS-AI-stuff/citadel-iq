using CitadelIQ.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>
/// One listener pass: registers every inbox message by (UIDVALIDITY, UID) — idempotently, so a restart or a crash
/// before archiving just re-registers the same key — supersedes unfinished rows from an older UIDVALIDITY, and moves
/// already-finished messages whose archive move failed earlier.
/// </summary>
public sealed class MailboxSynchronizer(
    IMailbox mailbox,
    IServiceScopeFactory scopes,
    IEmailAgentMonitor monitor,
    ILogger<MailboxSynchronizer> logger)
{
    public async Task<int> SyncAsync(CancellationToken cancellationToken)
    {
        var listing = await mailbox.ListInboxAsync(cancellationToken);
        monitor.RecordPoll();

        using var scope = scopes.CreateScope();
        var inbound = scope.ServiceProvider.GetRequiredService<IInboundEmailRepository>();

        var superseded = await inbound.SupersedeOldValidityAsync(listing.UidValidity, cancellationToken);
        var registered = 0;
        foreach (var entry in listing.Entries)
        {
            if (await inbound.RegisterAsync(entry.Key, entry.ReceivedAtUtc, cancellationToken))
            {
                registered++;
            }
        }

        var finished = await inbound.ListFinishedUidsAsync(listing.UidValidity, listing.Entries.Select(e => e.Key.Uid).ToList(), cancellationToken);
        foreach (var uid in finished)
        {
            try
            {
                await mailbox.ArchiveAsync(new MailboxKey(listing.UidValidity, uid), cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                // Best effort: the outcome is committed; the next sync tries again.
                logger.LogWarning("Re-archiving a finished message failed ({ExceptionType})", ex.GetType().Name);
            }
        }

        if (registered + superseded + finished.Count > 0)
        {
            logger.LogInformation("Mailbox sync: {Registered} new, {Superseded} superseded, {Rearchived} re-archived", registered, superseded, finished.Count);
        }

        return registered;
    }
}
