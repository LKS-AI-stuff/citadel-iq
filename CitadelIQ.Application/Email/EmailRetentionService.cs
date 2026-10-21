using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>Draft expiry and activity retention for one workspace (the scope must have entered it). The system
/// tables and the mailbox archive are purged separately, outside any workspace.</summary>
public class EmailRetentionService(
    IWorkspaceContext workspaceContext,
    IEmailDraftRepository drafts,
    IEmailQuestionRepository questions,
    IEmailThreadRepository threads,
    IOutboundEmailRepository outbound,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<EmailRetentionService> logger)
{
    public async Task RunAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var workspaceId = workspaceContext.WorkspaceId ?? throw new InvalidOperationException("No workspace entered.");
        var cutoff = nowUtc.AddDays(-options.CurrentValue.ActivityRetentionDays);

        var expired = await drafts.ExpireAsync(nowUtc, cancellationToken);
        var deletedQuestions = await questions.DeleteExpiredAsync(nowUtc, cancellationToken); // drafts and replies cascade
        var deletedOutbound = await outbound.DeleteOlderThanAsync(cutoff, cancellationToken);
        var deletedThreads = await threads.DeleteInactiveAsync(cutoff, cancellationToken);

        if (expired + deletedQuestions + deletedOutbound + deletedThreads > 0)
        {
            logger.LogInformation(
                "Email retention in workspace {WorkspaceId}: {Expired} drafts expired, {Questions} questions, {Outbound} outbound emails, {Threads} threads deleted",
                workspaceId, expired, deletedQuestions, deletedOutbound, deletedThreads);
        }
    }
}
