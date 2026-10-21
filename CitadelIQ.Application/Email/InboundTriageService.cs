using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>Result of scope A. <see cref="Sender"/> set ⇒ continue in the sender's workspace; otherwise the email was
/// already recorded in the operator log, and <see cref="Archive"/> says whether to move it out of the inbox.</summary>
public record InboundTriageOutcome(InboundEmail? Email, ActiveSender? Sender, bool Archive);

/// <summary>
/// Scope A — runs with no workspace, so row-level security shows no content rows. Fetches the message and decides
/// whether it may enter a workspace at all: not our own mail, not a duplicate, aligned SPF/DKIM, exactly one active
/// account with that address. Everything rejected here goes to the operator-only log (time, mailbox ids, reason,
/// sender domain — never the subject, body or full address).
/// </summary>
public class InboundTriageService(
    IMailbox mailbox,
    IInboundEmailRepository inbound,
    IUserAccountRepository users,
    AutomatedMailClassifier classifier,
    IOptionsMonitor<EmailAgentOptions> options,
    ILogger<InboundTriageService> logger)
{
    public async Task<InboundTriageOutcome> HandleAsync(InboundClaim claim, CancellationToken cancellationToken = default)
    {
        var email = await mailbox.FetchAsync(claim.Key, cancellationToken);
        if (email is null)
        {
            return await IgnoreAsync(claim, InboundIgnoreReason.Missing, null, null, archive: false, cancellationToken);
        }

        var domain = EmailAddresses.DomainOf(email.FromAddress);

        if (classifier.IsOwnMessage(email))
        {
            return await IgnoreAsync(claim, InboundIgnoreReason.Loop, domain, email, archive: true, cancellationToken);
        }

        if (email is { MessageIdSigned: true, MessageId: { } messageId }
            && await inbound.ExistsFinishedWithMessageIdAsync(EmailAddresses.HashMessageId(messageId), cancellationToken))
        {
            return await IgnoreAsync(claim, InboundIgnoreReason.Duplicate, domain, email, archive: true, cancellationToken);
        }

        if (!SenderAuthenticator.IsAuthenticated(email.FromAddress, email.TrustedAuthResults))
        {
            return await IgnoreAsync(claim, InboundIgnoreReason.Unauthenticated, domain, email, archive: true, cancellationToken);
        }

        var matches = await users.FindActiveByEmailAsync(email.FromAddress, options.CurrentValue.AllowDevelopmentIdentities, cancellationToken);
        if (matches.Count != 1)
        {
            var reason = matches.Count == 0 ? InboundIgnoreReason.UnknownSender : InboundIgnoreReason.AmbiguousSender;
            return await IgnoreAsync(claim, reason, domain, email, archive: true, cancellationToken);
        }

        return new InboundTriageOutcome(email, matches[0], Archive: false);
    }

    private async Task<InboundTriageOutcome> IgnoreAsync(
        InboundClaim claim, InboundIgnoreReason reason, string? senderDomain, InboundEmail? email, bool archive, CancellationToken cancellationToken)
    {
        if (!await inbound.IgnoreAsync(claim.Id, reason, senderDomain, cancellationToken))
        {
            throw new InboundEmailSupersededException();
        }

        logger.LogInformation("Inbound email {InboundEmailId} ignored: {Reason}", claim.Id, reason);
        return new InboundTriageOutcome(email, null, archive);
    }
}
