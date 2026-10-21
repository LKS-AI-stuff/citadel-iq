using CitadelIQ.Application.Common;
using CitadelIQ.Application.Email;
using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Tests.Support;

/// <summary>An in-memory support mailbox: INBOX, an archive folder and the Sent folder's Message-IDs, with a
/// UIDVALIDITY that tests can change. New mail is authenticated (aligned SPF pass) unless the test says otherwise.</summary>
public sealed class FakeMailbox : IMailbox
{
    private uint _nextUid = 1;
    private DateTimeOffset _lastReceived = DateTimeOffset.UnixEpoch;

    public uint UidValidity { get; private set; } = 1;
    public List<InboundEmail> Inbox { get; } = [];
    public List<InboundEmail> Archive { get; } = [];
    public HashSet<string> SentMessageIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every call throws <see cref="MailboxUnavailableException"/> (the mailbox is unreachable).</summary>
    public bool Unavailable { get; set; }

    /// <summary>Runs once, inside the next fetch (simulates something happening while an email is being processed).</summary>
    public Func<Task>? DuringNextFetch { get; set; }

    /// <summary>Archive moves throw (the outcome is committed, the message stays in the inbox).</summary>
    public bool FailArchive { get; set; }

    public InboundEmail Deliver(
        string from,
        string subject = "Question",
        string body = "What about revenue?",
        string? messageId = null,
        string? inReplyTo = null,
        IReadOnlyList<string>? references = null,
        IReadOnlyList<AuthResult>? auth = null,
        AutomationSignals? automation = null,
        bool messageIdSigned = false)
    {
        // Strictly increasing arrival times, so "oldest first" is deterministic.
        var received = DateTimeOffset.UtcNow;
        _lastReceived = received >= _lastReceived.AddMilliseconds(1) ? received : _lastReceived.AddMilliseconds(1);

        var email = new InboundEmail(
            new MailboxKey(UidValidity, _nextUid++),
            _lastReceived,
            messageId ?? $"{Guid.NewGuid():N}@{EmailAddresses.DomainOf(from) ?? "example.test"}",
            messageIdSigned,
            inReplyTo,
            references ?? (inReplyTo is null ? [] : [inReplyTo]),
            from,
            subject,
            body,
            auth ?? [new AuthResult("spf", "pass", EmailAddresses.DomainOf(from))],
            automation ?? AutomationSignals.None);
        Inbox.Add(email);
        return email;
    }

    /// <summary>The server reset UIDVALIDITY: every inbox message gets a new key.</summary>
    public void ChangeUidValidity()
    {
        UidValidity++;
        for (var i = 0; i < Inbox.Count; i++)
        {
            Inbox[i] = Inbox[i] with { Key = new MailboxKey(UidValidity, _nextUid++) };
        }
    }

    public Task<MailboxListing> ListInboxAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult(new MailboxListing(UidValidity, Inbox.Select(e => new MailboxEntry(e.Key, e.ReceivedAtUtc)).ToList()));
    }

    public async Task<InboundEmail?> FetchAsync(MailboxKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        var email = Inbox.FirstOrDefault(e => e.Key == key);
        if (DuringNextFetch is { } hook)
        {
            DuringNextFetch = null;
            await hook();
        }

        return email;
    }

    public Task ArchiveAsync(MailboxKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        if (FailArchive)
        {
            throw new MailboxUnavailableException("archive failed");
        }

        var email = Inbox.FirstOrDefault(e => e.Key == key);
        if (email is not null)
        {
            Inbox.Remove(email);
            Archive.Add(email);
        }

        return Task.CompletedTask;
    }

    public Task<bool> SentFolderContainsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult(SentMessageIds.Contains(messageId));
    }

    public Task WaitForChangeAsync(TimeSpan maxWait, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PurgeArchiveAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        Archive.RemoveAll(e => e.ReceivedAtUtc < olderThan);
        return Task.CompletedTask;
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new MailboxUnavailableException("mailbox down");
        }
    }
}

/// <summary>Records sent mail; like Gmail, it also "saves" each one to the fake Sent folder.</summary>
public sealed class FakeMailSender(FakeMailbox mailbox) : IMailSender
{
    public List<OutboundMessage> Sent { get; } = [];

    /// <summary>The next N sends throw <see cref="MailboxUnavailableException"/>.</summary>
    public int FailuresRemaining { get; set; }

    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw new MailboxUnavailableException("smtp down");
        }

        Sent.Add(message);
        mailbox.SentMessageIds.Add(message.MessageId);
        return Task.CompletedTask;
    }
}
