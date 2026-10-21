using System.Diagnostics;
using System.Text;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Rag;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>
/// Scope B — runs after <see cref="IWorkspaceContext.Enter"/> for the sender's workspace, so every content query is
/// confined to it by the query filters and row-level security. A bounded sequence, not a tool loop:
/// automated? → thread → caps → triage → [rewrite] → retrieve → [one reworded retry] → answer → judge → decide.
/// Everything is decided in memory and committed in one transaction (question, draft, outbound mail + queue rows,
/// thread counters, admin notifications, processing state), so a failure before the commit leaves nothing behind.
/// </summary>
public class EmailQuestionService(
    IWorkspaceContext workspaceContext,
    IEmailQuestionRepository questions,
    IEmailThreadRepository threads,
    IEmailDraftRepository drafts,
    IOutboundEmailRepository outbound,
    IOutboundEmailQueue queue,
    IInboundEmailRepository inbound,
    IFolderRepository folders,
    IAnswerService answers,
    EmailModelClient model,
    EmailBodyCleaner cleaner,
    AutomatedMailClassifier classifier,
    EmailReplyComposer composer,
    DraftNotificationService notifications,
    IUnitOfWork unitOfWork,
    IOptionsMonitor<EmailAgentOptions> options,
    IOptions<RagOptions> ragOptions,
    ILogger<EmailQuestionService> logger)
{
    private sealed class Plan(EmailQuestion question, ThreadMatch match)
    {
        public EmailQuestion Question { get; } = question;
        public ThreadMatch Match { get; } = match;
        public EmailThread? Thread { get; set; }
        public bool ThreadIsNew { get; set; }
        public EmailDraft? Draft { get; set; }
        public List<OutboundEmail> Outbound { get; } = [];
    }

    private sealed record DrainedAnswer(AnswerRun Run, string Text, bool NotFound, IReadOnlyList<int> Cited, bool Verified);

    public async Task HandleAsync(InboundClaim claim, ActiveSender sender, InboundEmail email, CancellationToken cancellationToken = default)
    {
        RequireSenderWorkspace(sender);
        var stopwatch = Stopwatch.StartNew();
        var o = options.CurrentValue;

        var plan = await StartPlanAsync(claim, sender, email, o, cancellationToken);
        await DecideAsync(plan, sender, email, o, cancellationToken);
        await CommitAsync(plan, claim, email, sender.Workspace.Id, failedWith: null, cancellationToken);

        logger.LogInformation(
            "Email question {QuestionId} (inbound {InboundEmailId}, workspace {WorkspaceId}): {Outcome} {Reason}, would auto-reply {WouldAutoReply}, " +
            "{ModelCalls} agent model calls ({InputTokens} in / {OutputTokens} out tokens), {OutboundCount} emails queued, {ElapsedMs} ms, {LatencySeconds} s since arrival",
            plan.Question.Id, claim.Id, sender.Workspace.Id, plan.Question.Outcome, plan.Question.Reason, plan.Question.WouldAutoReply,
            model.Calls, model.InputTokens, model.OutputTokens, plan.Outbound.Count, stopwatch.ElapsedMilliseconds,
            (int)(DateTimeOffset.UtcNow - email.ReceivedAtUtc).TotalSeconds);
    }

    /// <summary>
    /// After the retries ran out (or a non-transient error): an organization gets a "failed" draft with no suggestion
    /// plus a holding reply; an individual workspace gets the activity row only (requirements decision 21). No model call.
    /// </summary>
    public async Task RecordFailureAsync(InboundClaim claim, ActiveSender sender, InboundEmail email, string errorType, CancellationToken cancellationToken = default)
    {
        RequireSenderWorkspace(sender);
        var o = options.CurrentValue;
        var plan = await StartPlanAsync(claim, sender, email, o, cancellationToken);

        if (classifier.IsAutomated(email))
        {
            plan.Question.Ignore(EmailReason.AutomatedMessage);
        }
        else if (plan.Match.IsNotificationReply)
        {
            plan.Question.Ignore(EmailReason.NotificationReply);
        }
        else if (sender.Workspace.IsOrganization)
        {
            EnsureThread(plan, sender, email);
            Draft(plan, EmailReason.Failed, null, [], null, holdingReply: !plan.Thread!.HasReachedLimit(o.MaxAutomaticRepliesPerThread), o);
        }
        else
        {
            plan.Question.Fail();
        }

        await CommitAsync(plan, claim, email, sender.Workspace.Id, failedWith: errorType, cancellationToken);
        logger.LogWarning("Email question {QuestionId} (inbound {InboundEmailId}) recorded as failed ({ErrorType})", plan.Question.Id, claim.Id, errorType);
    }

    private async Task<Plan> StartPlanAsync(InboundClaim claim, ActiveSender sender, InboundEmail email, EmailAgentOptions o, CancellationToken cancellationToken)
    {
        var question = EmailQuestion.Create(
            sender.Workspace.Id, claim.Id, sender.User.Id, email.ReceivedAtUtc, email.Subject, email.MessageId, email.References,
            TimeSpan.FromDays(o.ActivityRetentionDays));

        var referencedIds = ThreadMatcher.ReferencedIds(email.InReplyTo, email.References);
        var referenced = referencedIds.Count == 0 ? [] : await outbound.FindByMessageIdsAsync(referencedIds, cancellationToken);
        var plan = new Plan(question, ThreadMatcher.Match(email.Subject, referenced, sender.User.Id, o.Texts.NotificationMarker));

        if (plan.Match.ThreadId is { } threadId)
        {
            plan.Thread = await threads.GetByIdAsync(threadId, cancellationToken);
        }

        return plan;
    }

    private async Task DecideAsync(Plan plan, ActiveSender sender, InboundEmail email, EmailAgentOptions o, CancellationToken cancellationToken)
    {
        var question = plan.Question;
        var isOrganization = sender.Workspace.IsOrganization;

        // 1. Automated mail (out-of-office, bounces, lists) and 2. replies to our draft notifications.
        if (classifier.IsAutomated(email))
        {
            question.Ignore(EmailReason.AutomatedMessage);
            return;
        }

        if (plan.Match.IsNotificationReply)
        {
            question.Ignore(EmailReason.NotificationReply);
            return;
        }

        // 3. Caps, before any model call (decision 19): a flood costs nothing and sends nothing.
        var now = DateTimeOffset.UtcNow;
        var usage = new EmailUsage(
            await questions.CountSinceAsync(sender.User.Id, now.AddHours(-1), cancellationToken),
            await questions.CountSinceAsync(null, now.AddDays(-1), cancellationToken),
            await inbound.CountProcessedSinceAsync(now.AddDays(-1), cancellationToken));
        if (EmailLimitPolicy.IsOverLimit(usage, o.Limits))
        {
            if (isOrganization)
            {
                EnsureThread(plan, sender, email);
                Draft(plan, EmailReason.OverLimit, null, [], null, holdingReply: false, o);
            }
            else
            {
                question.Ignore(EmailReason.OverLimit);
            }

            return;
        }

        // 4. Triage: is it a question, and what is it?
        var triage = await model.TriageAsync(email.Subject, cleaner.Clean(email.TextBody), cancellationToken);
        if (triage.Kind != TriageKind.Question)
        {
            if (plan.Thread is not null)
            {
                question.AttachToThread(plan.Thread);
                plan.Thread.Touch(); // retention deletes threads by last activity, cascading to their questions
            }

            question.Ignore(triage.Kind == TriageKind.Thanks ? EmailReason.Thanks : EmailReason.NotAQuestion);
            return;
        }

        question.SetQuestion(triage.Question);
        var threadRule = EmailLimitPolicy.ForThread(plan.Thread, o.MaxAutomaticRepliesPerThread);
        EnsureThread(plan, sender, email);

        if (threadRule == EmailReason.ThreadLimit)
        {
            if (isOrganization)
            {
                Draft(plan, EmailReason.ThreadLimit, null, [], null, holdingReply: false, o);
            }
            else
            {
                question.Ignore(EmailReason.ThreadLimit);
            }

            return;
        }

        if (!ragOptions.Value.Enabled)
        {
            if (isOrganization)
            {
                Draft(plan, EmailReason.AnswersDisabled, null, [], null, holdingReply: true, o);
            }
            else
            {
                question.Pause(EmailReason.AnswersDisabled);
            }

            return;
        }

        if (o.Mode != EmailAgentMode.On && !isOrganization)
        {
            question.Pause(EmailReason.DraftsOnly); // decision 20: nothing is sent
            return;
        }

        if (threadRule == EmailReason.AdminInThread && isOrganization)
        {
            Draft(plan, EmailReason.AdminInThread, null, [], null, holdingReply: true, o);
            return;
        }

        // 6. Answer with the same retrieval, grounding and citation checks as Ask.
        var answer = await AnswerAsync(plan, question.QuestionText!, o, cancellationToken);
        var sources = answer.Run.Sources
            .Select(s => new EmailSourceRef(s.Number, s.DocumentId, s.FolderId, s.FileName, s.PageNumber, s.SheetName, s.SimilarityScore, answer.Cited.Contains(s.Number)))
            .ToList();

        // 7. Judge (only a verified answer is worth judging), then 8. decide.
        JudgeResult? judge = answer.Verified
            ? await model.JudgeAsync(answer.Run.StandaloneQuestion, answer.Run.Sources, answer.Text, cancellationToken)
            : null;
        var gate = ConfidenceGate.Evaluate(answer.Verified, answer.Run.Sources, answer.Cited, judge?.Score, o);
        question.RecordGate(gate.Passes, judge?.Score);

        var suggestion = answer.NotFound || string.IsNullOrWhiteSpace(answer.Text)
            ? null
            : CitationParser.RemoveInvalid(answer.Text.Trim(), answer.Run.Sources.Count);

        if (gate.Passes && o.Mode == EmailAgentMode.On)
        {
            question.AutoReply(suggestion!, sources);
            AddReply(plan, OutboundEmailKind.AutoReply, composer.AutoReply(suggestion!, sources, sender.Workspace.Kind), o);
            return;
        }

        if (isOrganization)
        {
            var reason = suggestion is null ? EmailReason.NoAnswer : gate.Passes ? EmailReason.DraftsOnly : EmailReason.LowConfidence;
            Draft(plan, reason, suggestion, sources, judge?.Missing, holdingReply: true, o);
            return;
        }

        // Individual workspace, no reviewer: "no confident answer", with the suggestion only if labelled unverified.
        question.NoConfidentAnswer(suggestion is null ? EmailReason.NoAnswer : EmailReason.LowConfidence, suggestion, sources);
        AddReply(plan, OutboundEmailKind.NoConfidentReply, composer.NoConfidentReply(suggestion, sources), o);
    }

    private async Task<DrainedAnswer> AnswerAsync(Plan plan, string questionText, EmailAgentOptions o, CancellationToken cancellationToken)
    {
        var root = await folders.GetRootAsync(cancellationToken) ?? throw new InvalidOperationException("The workspace has no root folder.");
        var history = plan.ThreadIsNew ? [] : await BuildHistoryAsync(plan.Thread!.Id, o, cancellationToken);

        var run = await answers.StartAsync(new AskRequestDto(questionText, root.Id, SearchScope.EntireWorkspace, history), null, cancellationToken);

        if (o.MaxSearchAttempts > 1 && BestSimilarity(run.Sources) < o.MinCitedSimilarity)
        {
            var alternative = await model.RewordAsync(run.StandaloneQuestion, cancellationToken);
            if (alternative is not null && !string.Equals(alternative, run.StandaloneQuestion, StringComparison.OrdinalIgnoreCase))
            {
                run = await answers.StartAsync(
                    new AskRequestDto(run.StandaloneQuestion, root.Id, SearchScope.EntireWorkspace, history),
                    new AnswerRetrieval([alternative], QuestionIsStandalone: true),
                    cancellationToken);
            }
        }

        var text = new StringBuilder();
        var notFound = false;
        AnswerDoneEvent? done = null;
        await foreach (var e in run.StreamAsync(cancellationToken))
        {
            switch (e)
            {
                case AnswerTextEvent t:
                    text.Append(t.Delta);
                    break;
                case AnswerNotFoundEvent:
                    notFound = true;
                    break;
                case AnswerDoneEvent d:
                    done = d;
                    break;
            }
        }

        return new DrainedAnswer(run, text.ToString(), notFound, done?.CitedSources ?? [], done?.Verified ?? false);
    }

    /// <summary>Follow-up history from our own records of the thread — never the quoted text (decision 8): each
    /// question with the answer actually sent; rows without a reply are skipped. Trimmed to Ask's history limits,
    /// keeping the most recent turns.</summary>
    private async Task<IReadOnlyList<ConversationTurnDto>> BuildHistoryAsync(Guid threadId, EmailAgentOptions o, CancellationToken cancellationToken)
    {
        var turns = new List<ConversationTurnDto>();
        foreach (var entry in await questions.ListThreadAsync(threadId, cancellationToken))
        {
            var q = entry.Question;
            var answer = q.Outcome switch
            {
                EmailOutcome.AutoReplied => q.AnswerText,
                EmailOutcome.NoConfidentAnswer => o.Texts.NoConfidentReply,
                EmailOutcome.Drafted when entry.Draft is { Status: EmailDraftStatus.Sent } draft => draft.SentText,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(q.QuestionText) && !string.IsNullOrWhiteSpace(answer))
            {
                turns.Add(new ConversationTurnDto(q.QuestionText, answer));
            }
        }

        var rag = ragOptions.Value;
        var kept = new List<ConversationTurnDto>();
        var chars = 0;
        for (var i = turns.Count - 1; i >= 0 && kept.Count < rag.MaxHistoryTurns; i--)
        {
            var length = turns[i].Question.Length + turns[i].Answer.Length;
            if (chars + length > rag.MaxHistoryChars)
            {
                break;
            }

            chars += length;
            kept.Insert(0, turns[i]);
        }

        return kept;
    }

    private static double BestSimilarity(IReadOnlyList<AnswerSourceDto> sources) =>
        sources.Count == 0 ? 0 : sources.Max(s => s.SimilarityScore);

    private static void EnsureThread(Plan plan, ActiveSender sender, InboundEmail email)
    {
        if (plan.Thread is null)
        {
            plan.Thread = EmailThread.Create(sender.Workspace.Id, sender.User.Id, email.Subject);
            plan.ThreadIsNew = true;
        }
        else
        {
            plan.Thread.Touch();
        }

        plan.Question.AttachToThread(plan.Thread);
    }

    private void Draft(Plan plan, EmailReason reason, string? suggestion, IReadOnlyList<EmailSourceRef> sources, string? missing, bool holdingReply, EmailAgentOptions o)
    {
        plan.Question.Draft(reason, suggestion, sources);
        plan.Draft = EmailDraft.Create(plan.Question, reason, suggestion, missing, TimeSpan.FromDays(o.DraftExpiryDays));

        if (holdingReply && !plan.Thread!.HasReachedLimit(o.MaxAutomaticRepliesPerThread))
        {
            AddReply(plan, OutboundEmailKind.HoldingReply, composer.HoldingReply(), o);
        }
    }

    /// <summary>Queues a reply to the asker in the thread. The recipient is the user id; the address is read from the
    /// user record at send time — never from Reply-To, CC or the body.</summary>
    private void AddReply(Plan plan, OutboundEmailKind kind, string body, EmailAgentOptions o)
    {
        var question = plan.Question;
        var thread = plan.Thread!;
        thread.RecordAutomaticReply(o.MaxAutomaticRepliesPerThread);

        plan.Outbound.Add(OutboundEmail.Create(
            question.WorkspaceId, kind, question.SenderUserId,
            EmailReplyComposer.ReplySubject(question.Subject), body, composer.NewMessageId(),
            question.InboundMessageId, EmailReplyComposer.ReplyReferences(question.InboundMessageId, question.InboundReferences),
            thread.Id, question.Id));
    }

    private async Task CommitAsync(Plan plan, InboundClaim claim, InboundEmail email, Guid workspaceId, string? failedWith, CancellationToken cancellationToken)
    {
        var messageIdHash = email is { MessageIdSigned: true, MessageId: { } id } ? EmailAddresses.HashMessageId(id) : null;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            bool finished;
            if (plan.Thread is not null)
            {
                if (plan.ThreadIsNew)
                {
                    await threads.AddAsync(plan.Thread, cancellationToken);
                }
                else
                {
                    await threads.UpdateAsync(plan.Thread, cancellationToken);
                }
            }

            await questions.AddAsync(plan.Question, cancellationToken);

            if (plan.Draft is not null)
            {
                await drafts.AddAsync(plan.Draft, cancellationToken);
                await notifications.ScheduleAsync(workspaceId, cancellationToken);
            }

            foreach (var message in plan.Outbound)
            {
                await outbound.AddAsync(message, cancellationToken);
                await queue.EnqueueAsync(workspaceId, message.Id, cancellationToken);
            }

            if (failedWith is not null && plan.Question.Outcome is EmailOutcome.Failed or EmailOutcome.Drafted)
            {
                finished = await inbound.FailAsync(claim.Id, workspaceId, failedWith, cancellationToken);
            }
            else
            {
                finished = await inbound.CompleteAsync(claim.Id, workspaceId, messageIdHash, cancellationToken);
            }

            if (!finished)
            {
                throw new InboundEmailSupersededException(); // rolls back everything above: no reply is queued twice
            }
        }, cancellationToken);
    }

    private void RequireSenderWorkspace(ActiveSender sender)
    {
        if (workspaceContext.WorkspaceId != sender.Workspace.Id)
        {
            throw new InvalidOperationException("Enter the sender's workspace before handling their email.");
        }
    }
}
