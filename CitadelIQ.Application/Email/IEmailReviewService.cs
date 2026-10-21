using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Email;

/// <summary>The portal side of the email agent. Draft and notification methods need Owner/Admin in an organization
/// (individual workspaces get a 404, like the organization endpoints); activity is open to every member, who sees
/// only their own emails unless Owner/Admin.</summary>
public interface IEmailReviewService
{
    Task<IReadOnlyList<EmailDraftSummaryDto>> ListDraftsAsync(EmailDraftStatus status, CancellationToken cancellationToken = default);

    Task<EmailDraftDto> GetDraftAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PendingDraftCountDto> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Queues the (possibly edited) reply to the asker, in the original thread.</summary>
    Task SendDraftAsync(Guid id, string? text, CancellationToken cancellationToken = default);

    Task DismissDraftAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Newest first. Next page: <paramref name="before"/> = the last row's time, <paramref name="skip"/> = how many
    /// rows already returned have exactly that time.</summary>
    Task<IReadOnlyList<EmailActivityDto>> ListActivityAsync(DateTimeOffset? before, int? limit, bool mine, int skip = 0, CancellationToken cancellationToken = default);

    Task<EmailNotificationPreferenceDto> GetNotificationPreferenceAsync(CancellationToken cancellationToken = default);

    Task SetNotificationPreferenceAsync(bool emailOnNewDrafts, CancellationToken cancellationToken = default);
}

public interface IEmailStatusService
{
    Task<EmailStatusDto> GetAsync(CancellationToken cancellationToken = default);
}
