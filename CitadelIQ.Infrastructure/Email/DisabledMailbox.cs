using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>Registered when <c>EmailAgent:Mailbox:Host</c> is empty: no hosted loop runs, so this is never called; it
/// only keeps the email services constructible (the portal endpoints still work).</summary>
public sealed class DisabledMailbox : IMailbox, IMailSender
{
    private static InvalidOperationException NotConfigured() =>
        new("The support mailbox is not configured (EmailAgent:Mailbox:Host).");

    public Task<MailboxListing> ListInboxAsync(CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<InboundEmail?> FetchAsync(MailboxKey key, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task ArchiveAsync(MailboxKey key, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<bool> SentFolderContainsAsync(string messageId, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task WaitForChangeAsync(TimeSpan maxWait, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task PurgeArchiveAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default) => throw NotConfigured();
}
