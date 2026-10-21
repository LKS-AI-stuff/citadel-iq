using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

public class EmailReviewService(
    ICurrentUser currentUser,
    IEmailDraftRepository drafts,
    IEmailQuestionRepository questions,
    IEmailThreadRepository threads,
    IOutboundEmailRepository outbound,
    IOutboundEmailQueue queue,
    IUserAccountRepository users,
    IMembershipRepository memberships,
    EmailReplyComposer composer,
    IUnitOfWork unitOfWork,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<EmailReviewService> logger) : IEmailReviewService
{
    public const string AlreadyHandledMessage = "This draft was already sent or dismissed.";
    public const string AskerLeftMessage = "This person is no longer a member of the organization, so the reply can't be sent.";

    private const string ReviewAction = "review email drafts";
    private const int DefaultActivityLimit = 50;
    private const int MaxActivityLimit = 100;
    private const int MaxActivitySkip = 10_000;
    private const int MaxDraftListSize = 200;

    public async Task<IReadOnlyList<EmailDraftSummaryDto>> ListDraftsAsync(EmailDraftStatus status, CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException("Unrecognized draft status.");
        }

        var records = await drafts.ListAsync(status, MaxDraftListSize, cancellationToken);
        return records.Select(r => new EmailDraftSummaryDto(
                r.Draft.Id, r.Question.Id, new EmailPersonDto(r.Question.SenderUserId, r.AskerDisplayName),
                r.Question.Subject, r.Draft.Reason, r.Draft.Status, r.Draft.CreatedAtUtc))
            .ToList();
    }

    public async Task<EmailDraftDto> GetDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        var r = await drafts.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Draft not found.");
        return new EmailDraftDto(
            r.Draft.Id, r.Question.Id, r.Draft.Status, r.Draft.Reason,
            new EmailPersonDto(r.Question.SenderUserId, r.AskerDisplayName),
            r.Question.Subject, r.Question.QuestionText, r.Question.ReceivedAtUtc,
            r.Draft.SuggestedText, r.Draft.Missing, r.Draft.SentText, r.Question.Confidence,
            r.Question.Sources.OrderBy(s => s.Number)
                .Select(s => new EmailDraftSourceDto(s.Number, s.DocumentId, s.FolderId, s.FileName, s.PageNumber, s.SheetName, s.Cited))
                .ToList(),
            r.Draft.CreatedAtUtc, r.Draft.ExpiresAtUtc);
    }

    public async Task<PendingDraftCountDto> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        return new PendingDraftCountDto(await drafts.CountPendingAsync(null, cancellationToken));
    }

    public async Task SendDraftAsync(Guid id, string? text, CancellationToken cancellationToken = default)
    {
        var workspace = RequireReviewer();
        var actorId = currentUser.RequireUserId();
        var maxChars = options.CurrentValue.MaxDraftChars;

        var reply = text?.Trim() ?? "";
        if (reply.Length == 0)
        {
            throw new ValidationException("The reply can't be empty.");
        }

        if (reply.Length > maxChars)
        {
            throw new ValidationException($"The reply is too long. Keep it under {maxChars} characters.");
        }

        var record = await drafts.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Draft not found.");
        if (record.Draft.Status != EmailDraftStatus.Pending)
        {
            throw new ValidationException(AlreadyHandledMessage);
        }

        var question = record.Question;
        var asker = await users.GetByIdAsync(question.SenderUserId, cancellationToken);
        var askerMembership = await memberships.GetByUserIdAsync(question.SenderUserId, cancellationToken);
        if (asker is null || asker.IsClosed || string.IsNullOrWhiteSpace(asker.Email) || askerMembership?.WorkspaceId != workspace.Id)
        {
            throw new ValidationException(AskerLeftMessage);
        }

        record.Draft.Send(actorId, reply);
        var thread = question.ThreadId is { } threadId ? await threads.GetByIdAsync(threadId, cancellationToken) : null;
        var message = OutboundEmail.Create(
            workspace.Id, OutboundEmailKind.DraftReply, asker.Id,
            EmailReplyComposer.ReplySubject(question.Subject), composer.DraftReply(reply, question.Sources), composer.NewMessageId(),
            question.InboundMessageId, EmailReplyComposer.ReplyReferences(question.InboundMessageId, question.InboundReferences),
            thread?.Id, question.Id);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Conditional Pending → Sent: of two reviewers sending at once, exactly one wins.
            if (!await drafts.TrySaveDecisionAsync(record.Draft, cancellationToken))
            {
                throw new ValidationException(AlreadyHandledMessage);
            }

            await outbound.AddAsync(message, cancellationToken);
            await queue.EnqueueAsync(workspace.Id, message.Id, cancellationToken);

            if (thread is not null)
            {
                thread.MarkAdminReplied(); // later messages in this thread become drafts
                await threads.UpdateAsync(thread, cancellationToken);
            }
        }, cancellationToken);

        logger.LogInformation("{ActorId} sent email draft {DraftId}", actorId, id);
    }

    public async Task DismissDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        var actorId = currentUser.RequireUserId();
        var record = await drafts.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Draft not found.");
        if (record.Draft.Status != EmailDraftStatus.Pending)
        {
            throw new ValidationException(AlreadyHandledMessage);
        }

        record.Draft.Dismiss(actorId);
        if (!await drafts.TrySaveDecisionAsync(record.Draft, cancellationToken))
        {
            throw new ValidationException(AlreadyHandledMessage);
        }

        logger.LogInformation("{ActorId} dismissed email draft {DraftId}", actorId, id);
    }

    public async Task<IReadOnlyList<EmailActivityDto>> ListActivityAsync(DateTimeOffset? before, int? limit, bool mine, int skip = 0, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsActiveMember)
        {
            throw new ForbiddenException("You don't have permission to view email activity.");
        }

        var userId = currentUser.RequireUserId();
        var seesWholeWorkspace = currentUser.Workspace!.IsOrganization && currentUser.Role >= WorkspaceRole.Admin;
        var size = Math.Clamp(limit ?? DefaultActivityLimit, 1, MaxActivityLimit);

        if (skip is < 0 or > MaxActivitySkip)
        {
            throw new ValidationException("Invalid page position.");
        }

        var records = await questions.ListActivityAsync(before, skip, size, seesWholeWorkspace && !mine ? null : userId, cancellationToken);
        return records.Select(r =>
        {
            var q = r.Question;
            var used = q.Sources.Any(s => s.Cited) ? q.Sources.Where(s => s.Cited) : q.Sources;
            return new EmailActivityDto(
                q.Id, q.ReceivedAtUtc, new EmailPersonDto(q.SenderUserId, r.SenderDisplayName), q.Subject, q.Outcome, q.Reason, q.Confidence,
                used.DistinctBy(s => s.DocumentId).Select(s => new EmailDocumentDto(s.DocumentId, s.FolderId, s.FileName)).ToList(),
                r.Delivery == OutboundEmailStatus.Sending ? OutboundEmailStatus.Pending : r.Delivery);
        }).ToList();
    }

    public Task<EmailNotificationPreferenceDto> GetNotificationPreferenceAsync(CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        return Task.FromResult(new EmailNotificationPreferenceDto(currentUser.Membership!.EmailDraftNotifications));
    }

    public async Task SetNotificationPreferenceAsync(bool emailOnNewDrafts, CancellationToken cancellationToken = default)
    {
        RequireReviewer();
        await memberships.SetEmailDraftNotificationsAsync(currentUser.RequireUserId(), emailOnNewDrafts, cancellationToken);
    }

    /// <summary>Drafts exist only in organizations; individual workspaces get a 404 like the organization endpoints.</summary>
    private Workspace RequireReviewer()
    {
        if (currentUser.Workspace is not { Kind: WorkspaceKind.Organization } workspace || !currentUser.IsActiveMember)
        {
            throw new NotFoundException("Organization not found.");
        }

        currentUser.EnsureRole(WorkspaceRole.Admin, ReviewAction);
        return workspace;
    }
}
