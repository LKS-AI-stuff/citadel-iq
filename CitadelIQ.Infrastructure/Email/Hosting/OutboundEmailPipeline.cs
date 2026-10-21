using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Email;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email.Hosting;

/// <summary>Sends one claimed outbound email in a scope that has entered its workspace; retries transient failures
/// with backoff and records the email as failed when the retries run out.</summary>
public sealed class OutboundEmailPipeline(
    IServiceScopeFactory scopes,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<OutboundEmailPipeline> logger)
{
    /// <summary>False when nothing was due.</summary>
    public async Task<bool> SendNextAsync(CancellationToken cancellationToken)
    {
        var o = options.CurrentValue;
        OutboundClaim? claim;
        using (var scope = scopes.CreateScope())
        {
            claim = await scope.ServiceProvider.GetRequiredService<IOutboundEmailQueue>()
                .ClaimNextAsync(TimeSpan.FromMinutes(o.ProcessingLeaseMinutes), cancellationToken);
        }

        if (claim is null)
        {
            return false;
        }

        try
        {
            using var scope = CreateWorkspaceScope(claim.WorkspaceId);
            await scope.ServiceProvider.GetRequiredService<OutboundSendService>().SendAsync(claim, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the lease expires; the Sent-folder check prevents a duplicate
        }
        catch (Exception ex)
        {
            if (EmailRetryPolicy.IsTransient(ex) && claim.Attempt < o.MaxAttempts)
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IOutboundEmailQueue>()
                    .ScheduleRetryAsync(claim.OutboundEmailId, DateTimeOffset.UtcNow + o.RetryDelay(claim.Attempt), cancellationToken);
                logger.LogWarning("Outbound email {OutboundEmailId} attempt {Attempt} failed ({ErrorType}); retrying",
                    claim.OutboundEmailId, claim.Attempt, ex.GetType().Name);
            }
            else
            {
                using var scope = CreateWorkspaceScope(claim.WorkspaceId);
                await scope.ServiceProvider.GetRequiredService<OutboundSendService>().MarkFailedAsync(claim, cancellationToken);
            }
        }

        return true;
    }

    private IServiceScope CreateWorkspaceScope(Guid workspaceId)
    {
        var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContext>().Enter(workspaceId);
        return scope;
    }
}
