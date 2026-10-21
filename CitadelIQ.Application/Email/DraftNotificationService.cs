using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>
/// Batched "drafts awaiting review" emails to Owners/Admins (requirements decision 14): at most one per admin per
/// interval, nothing if the drafts were already handled, and never the question or answer — only asker names,
/// subjects, reasons and a link to the review queue.
/// </summary>
public class DraftNotificationService(
    IWorkspaceContext workspaceContext,
    IMembershipRepository memberships,
    IUserAccountRepository users,
    IAdminNotificationSchedule schedule,
    IEmailDraftRepository drafts,
    IOutboundEmailRepository outbound,
    IOutboundEmailQueue queue,
    EmailReplyComposer composer,
    IUnitOfWork unitOfWork,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<DraftNotificationService> logger)
{
    /// <summary>A draft was created: schedule a notification for every opted-in Owner/Admin. Runs inside the draft's
    /// transaction.</summary>
    public async Task ScheduleAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromMinutes(options.CurrentValue.NotificationIntervalMinutes);
        foreach (var member in await memberships.ListByWorkspaceAsync(workspaceId, cancellationToken))
        {
            if (member.Membership.Role >= WorkspaceRole.Admin && member.Membership.EmailDraftNotifications && !member.User.IsClosed)
            {
                await schedule.ScheduleAsync(member.User.Id, workspaceId, interval, cancellationToken);
            }
        }
    }

    /// <summary>Sends the admin's due notification, if still needed. The scope must have entered the admin's workspace.</summary>
    public async Task SendDueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var workspaceId = workspaceContext.WorkspaceId ?? throw new InvalidOperationException("No workspace entered.");
        var now = DateTimeOffset.UtcNow;
        var sent = false;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entry = await schedule.LockDueAsync(userId, now, cancellationToken);
            if (entry is null)
            {
                return;
            }

            var membership = await memberships.GetByUserIdAsync(userId, cancellationToken);
            var user = await users.GetByIdAsync(userId, cancellationToken);
            if (entry.WorkspaceId != workspaceId
                || membership is not { Role: >= WorkspaceRole.Admin, EmailDraftNotifications: true }
                || membership.WorkspaceId != workspaceId
                || user is null || user.IsClosed || string.IsNullOrWhiteSpace(user.Email))
            {
                await schedule.MarkHandledAsync(userId, null, cancellationToken);
                return;
            }

            // A draft's CreatedAtUtc is stamped before its transaction commits, so one created just before the last
            // notification may have committed just after it. Look back one processing lease so it is never missed
            // (at worst a still-pending draft is listed in two notifications).
            var since = entry.LastSentAtUtc - TimeSpan.FromMinutes(options.CurrentValue.ProcessingLeaseMinutes);
            var count = await drafts.CountPendingAsync(since, cancellationToken);
            if (count == 0)
            {
                await schedule.MarkHandledAsync(userId, null, cancellationToken); // already handled
                return;
            }

            var pending = await drafts.ListPendingAsync(since, EmailReplyComposer.MaxNotificationItems, cancellationToken);
            var (subject, body) = composer.DraftNotification(count,
                pending.Select(d => new NotificationItem(d.AskerDisplayName, d.Question.Subject, d.Draft.Reason)).ToList());

            var email = OutboundEmail.Create(workspaceId, OutboundEmailKind.DraftNotification, userId, subject, body, composer.NewMessageId());
            await outbound.AddAsync(email, cancellationToken);
            await queue.EnqueueAsync(workspaceId, email.Id, cancellationToken);
            await schedule.MarkHandledAsync(userId, now, cancellationToken);
            sent = true;
        }, cancellationToken);

        if (sent)
        {
            logger.LogInformation("Draft notification queued for admin {UserId} in workspace {WorkspaceId}", userId, workspaceId);
        }
    }
}
