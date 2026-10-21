using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Enums;

namespace CitadelIQ.Application.Dtos;

/// <summary>The portal's support line. <see cref="SupportAddress"/> is null while the agent is off.
/// <see cref="Reason"/>: <c>NoEmail</c> or <c>SharedAddress</c> when <see cref="CanUse"/> is false.</summary>
public record EmailStatusDto(string? SupportAddress, EmailAgentMode Mode, string YourAddress, bool CanUse, string? Reason);

public record EmailPersonDto(Guid UserId, string DisplayName);

public record EmailDocumentDto(Guid DocumentId, Guid FolderId, string FileName);

/// <summary><see cref="Delivery"/>: the reply's state — Pending, Sent, Failed or Cancelled; null when nothing was sent.</summary>
public record EmailActivityDto(
    Guid Id,
    DateTimeOffset ReceivedAtUtc,
    EmailPersonDto Sender,
    string Subject,
    EmailOutcome Outcome,
    EmailReason? Reason,
    double? Confidence,
    IReadOnlyList<EmailDocumentDto> Documents,
    OutboundEmailStatus? Delivery);

public record EmailDraftSummaryDto(
    Guid Id,
    Guid QuestionId,
    EmailPersonDto Asker,
    string Subject,
    EmailReason Reason,
    EmailDraftStatus Status,
    DateTimeOffset CreatedAtUtc);

public record EmailDraftSourceDto(int Number, Guid DocumentId, Guid FolderId, string FileName, int? Page, string? Sheet, bool Cited);

public record EmailDraftDto(
    Guid Id,
    Guid QuestionId,
    EmailDraftStatus Status,
    EmailReason Reason,
    EmailPersonDto Asker,
    string Subject,
    string? Question,
    DateTimeOffset ReceivedAtUtc,
    string? SuggestedText,
    string? Missing,
    string? SentText,
    double? Confidence,
    IReadOnlyList<EmailDraftSourceDto> Sources,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public record PendingDraftCountDto(int Pending);

public record EmailNotificationPreferenceDto(bool EmailOnNewDrafts);
