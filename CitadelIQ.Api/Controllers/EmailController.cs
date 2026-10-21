using CitadelIQ.Api.Contracts;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Email;
using CitadelIQ.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

/// <summary>The email agent in the portal: the support line, the review queue and email activity. Role checks (Owner/
/// Admin for drafts and notifications) and workspace isolation are enforced by the services.</summary>
[ApiController]
[Route("api/email")]
public class EmailController(IEmailStatusService statusService, IEmailReviewService reviewService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<EmailStatusDto>> GetStatus(CancellationToken cancellationToken) =>
        Ok(await statusService.GetAsync(cancellationToken));

    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyList<EmailActivityDto>>> ListActivity(
        [FromQuery] DateTimeOffset? before, [FromQuery] int? limit, [FromQuery] bool mine, [FromQuery] int skip, CancellationToken cancellationToken) =>
        Ok(await reviewService.ListActivityAsync(before, limit, mine, skip, cancellationToken));

    [HttpGet("drafts")]
    public async Task<ActionResult<IReadOnlyList<EmailDraftSummaryDto>>> ListDrafts([FromQuery] EmailDraftStatus status, CancellationToken cancellationToken) =>
        Ok(await reviewService.ListDraftsAsync(status, cancellationToken));

    [HttpGet("drafts/count")]
    public async Task<ActionResult<PendingDraftCountDto>> CountPendingDrafts(CancellationToken cancellationToken) =>
        Ok(await reviewService.CountPendingAsync(cancellationToken));

    [HttpGet("drafts/{draftId:guid}")]
    public async Task<ActionResult<EmailDraftDto>> GetDraft(Guid draftId, CancellationToken cancellationToken) =>
        Ok(await reviewService.GetDraftAsync(draftId, cancellationToken));

    [HttpPost("drafts/{draftId:guid}/send")]
    [RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> SendDraft(Guid draftId, [FromBody] SendDraftRequest request, CancellationToken cancellationToken)
    {
        await reviewService.SendDraftAsync(draftId, request.Text, cancellationToken);
        return NoContent();
    }

    [HttpPost("drafts/{draftId:guid}/dismiss")]
    public async Task<IActionResult> DismissDraft(Guid draftId, CancellationToken cancellationToken)
    {
        await reviewService.DismissDraftAsync(draftId, cancellationToken);
        return NoContent();
    }

    [HttpGet("notification-preference")]
    public async Task<ActionResult<EmailNotificationPreferenceDto>> GetNotificationPreference(CancellationToken cancellationToken) =>
        Ok(await reviewService.GetNotificationPreferenceAsync(cancellationToken));

    [HttpPut("notification-preference")]
    public async Task<IActionResult> SetNotificationPreference([FromBody] NotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        await reviewService.SetNotificationPreferenceAsync(
            request.EmailOnNewDrafts ?? throw new Application.Common.ValidationException("emailOnNewDrafts is required."), cancellationToken);
        return NoContent();
    }
}
