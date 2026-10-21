using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Email;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>Due draft notifications, and retention: per workspace (each in its own scope, entered first), then the
/// system tables and the mailbox's archive folder.</summary>
public sealed class EmailMaintenanceRunner(
    IServiceScopeFactory scopes,
    IMailbox mailbox,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<EmailMaintenanceRunner> logger)
{
    private const int NotificationBatchSize = 100;

    public async Task<int> SendDueNotificationsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DueNotification> due;
        using (var scope = scopes.CreateScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IAdminNotificationSchedule>()
                .ListDueAsync(DateTimeOffset.UtcNow, NotificationBatchSize, cancellationToken);
        }

        foreach (var entry in due)
        {
            try
            {
                using var scope = CreateWorkspaceScope(entry.WorkspaceId);
                await scope.ServiceProvider.GetRequiredService<DraftNotificationService>().SendDueAsync(entry.UserId, cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                logger.LogWarning("Draft notification for admin {UserId} failed ({ExceptionType}); it stays due", entry.UserId, ex.GetType().Name);
            }
        }

        return due.Count;
    }

    public async Task RunRetentionAsync(bool purgeArchive, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-options.CurrentValue.ActivityRetentionDays);

        IReadOnlyList<Guid> workspaceIds;
        using (var scope = scopes.CreateScope())
        {
            workspaceIds = await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().ListIdsAsync(cancellationToken);
        }

        foreach (var workspaceId in workspaceIds)
        {
            try
            {
                using var scope = CreateWorkspaceScope(workspaceId);
                await scope.ServiceProvider.GetRequiredService<EmailRetentionService>().RunAsync(now, cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                logger.LogWarning("Email retention for workspace {WorkspaceId} failed ({ExceptionType})", workspaceId, ex.GetType().Name);
            }
        }

        using (var scope = scopes.CreateScope())
        {
            var inbound = await scope.ServiceProvider.GetRequiredService<IInboundEmailRepository>().DeleteOlderThanAsync(cutoff, cancellationToken);
            var queued = await scope.ServiceProvider.GetRequiredService<IOutboundEmailQueue>().DeleteFinishedOlderThanAsync(cutoff, cancellationToken);
            if (inbound + queued > 0)
            {
                logger.LogInformation("Email retention: {Inbound} processing rows and {Queued} queue rows deleted", inbound, queued);
            }
        }

        if (purgeArchive)
        {
            await mailbox.PurgeArchiveAsync(cutoff, cancellationToken);
        }
    }

    private IServiceScope CreateWorkspaceScope(Guid workspaceId)
    {
        var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContext>().Enter(workspaceId);
        return scope;
    }
}
