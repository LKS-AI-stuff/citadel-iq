using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Email;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>
/// Processes one claimed inbound email: scope A (no workspace) decides whether the sender may enter a workspace at
/// all; scope B enters that workspace before anything touches its DbContext and runs the question pipeline. The
/// message is archived only after its outcome is committed. Failures are retried with backoff; when the retries run
/// out the outcome is recorded as failed — never a partial reply.
/// </summary>
public sealed class InboundEmailPipeline(
    IServiceScopeFactory scopes,
    IMailbox mailbox,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<InboundEmailPipeline> logger)
{
    /// <summary>False when nothing was due.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var o = options.CurrentValue;
        InboundClaim? claim;
        using (var scope = scopes.CreateScope())
        {
            claim = await scope.ServiceProvider.GetRequiredService<IInboundEmailRepository>()
                .ClaimNextAsync(TimeSpan.FromMinutes(o.ProcessingLeaseMinutes), cancellationToken);
        }

        if (claim is null)
        {
            return false;
        }

        InboundTriageOutcome? triage = null;
        try
        {
            using (var scopeA = scopes.CreateScope())
            {
                triage = await scopeA.ServiceProvider.GetRequiredService<InboundTriageService>().HandleAsync(claim, cancellationToken);
            }

            if (triage.Sender is { } sender)
            {
                using var scopeB = CreateWorkspaceScope(sender.Workspace.Id);
                await scopeB.ServiceProvider.GetRequiredService<EmailQuestionService>().HandleAsync(claim, sender, triage.Email!, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // shutting down: the lease expires and the email is processed again
        }
        catch (InboundEmailSupersededException)
        {
            // A UIDVALIDITY change re-registered this message under a new key mid-flight; that row handles it.
            logger.LogInformation("Inbound email {InboundEmailId} was superseded while processing; nothing committed", claim.Id);
            return true;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(claim, triage, ex, o, cancellationToken);
            return true;
        }

        if (triage.Sender is not null || triage.Archive)
        {
            await ArchiveAsync(claim, cancellationToken);
        }

        return true;
    }

    private async Task HandleFailureAsync(InboundClaim claim, InboundTriageOutcome? triage, Exception ex, EmailAgentOptions o, CancellationToken cancellationToken)
    {
        var errorType = ex.GetType().Name;
        using var scope = scopes.CreateScope();
        var inbound = scope.ServiceProvider.GetRequiredService<IInboundEmailRepository>();

        // The mailbox itself is unreachable: pause this email instead of spending an attempt (it resumes, in order,
        // once access is restored).
        if (ex is MailboxUnavailableException && triage is null && !await MailboxAnswersAsync(cancellationToken))
        {
            await inbound.ScheduleRetryAsync(claim.Id, DateTimeOffset.UtcNow + o.RetryDelay(1), errorType, countAttempt: false, cancellationToken);
            logger.LogWarning("Inbound email {InboundEmailId} postponed: mailbox unavailable", claim.Id);
            return;
        }

        if (EmailRetryPolicy.IsTransient(ex) && claim.Attempt < o.MaxAttempts)
        {
            await inbound.ScheduleRetryAsync(claim.Id, DateTimeOffset.UtcNow + o.RetryDelay(claim.Attempt), errorType, countAttempt: true, cancellationToken);
            logger.LogWarning("Inbound email {InboundEmailId} attempt {Attempt} failed ({ErrorType}); retrying", claim.Id, claim.Attempt, errorType);
            return;
        }

        if (triage?.Sender is { } sender)
        {
            try
            {
                using var scopeB = CreateWorkspaceScope(sender.Workspace.Id);
                await scopeB.ServiceProvider.GetRequiredService<EmailQuestionService>()
                    .RecordFailureAsync(claim, sender, triage.Email!, errorType, cancellationToken);
                await ArchiveAsync(claim, cancellationToken);
                return;
            }
            catch (InboundEmailSupersededException)
            {
                logger.LogInformation("Inbound email {InboundEmailId} was superseded while processing; nothing committed", claim.Id);
                return;
            }
            catch (Exception inner) when (!(inner is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                logger.LogError("Recording the failure of inbound email {InboundEmailId} failed ({ExceptionType})", claim.Id, inner.GetType().Name);
            }
        }

        if (!await inbound.FailAsync(claim.Id, triage?.Sender?.Workspace.Id, errorType, cancellationToken))
        {
            return; // superseded: the message is handled under its new key
        }

        logger.LogError("Inbound email {InboundEmailId} failed after {Attempt} attempts ({ErrorType})", claim.Id, claim.Attempt, errorType);
        await ArchiveAsync(claim, cancellationToken);
    }

    /// <summary>Whether the mailbox itself is reachable: if it is, a failing fetch is about this message, so the attempt
    /// counts (and a poison message eventually fails) instead of being postponed forever.</summary>
    private async Task<bool> MailboxAnswersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await mailbox.ListInboxAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return false;
        }
    }

    /// <summary>Best effort: the outcome is already committed, and the listener re-archives finished mail it still sees.</summary>
    private async Task ArchiveAsync(InboundClaim claim, CancellationToken cancellationToken)
    {
        try
        {
            await mailbox.ArchiveAsync(claim.Key, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("Archiving inbound email {InboundEmailId} failed ({ExceptionType}); will retry on the next sync", claim.Id, ex.GetType().Name);
        }
    }

    /// <summary>The workspace is entered before anything resolves the DbContext, so its connection is stamped for RLS.</summary>
    private IServiceScope CreateWorkspaceScope(Guid workspaceId)
    {
        var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContext>().Enter(workspaceId);
        return scope;
    }
}
