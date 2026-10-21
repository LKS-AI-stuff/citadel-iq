using System.Net.Sockets;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>
/// The support mailbox over IMAP (MailKit). Two connections: one long-lived for IDLE (IDLE blocks its connection) and
/// one for commands, serialized by a lock and reconnected on failure. Connection-level failures surface as
/// <see cref="MailboxUnavailableException"/>; protocol logging stays off so no credentials or content reach the logs.
/// </summary>
public sealed class ImapMailbox(
    IOptionsMonitor<MailboxOptions> options,
    IOptionsMonitor<EmailAgentOptions> agentOptions,
    IMailCredentialProvider credentials,
    ILogger<ImapMailbox> logger) : IMailbox, IAsyncDisposable
{
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly SemaphoreSlim _idleLock = new(1, 1);
    private ImapClient? _commandClient;
    private ImapClient? _idleClient;

    public Task<MailboxListing> ListInboxAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async client =>
        {
            var inbox = await OpenInboxAsync(client, FolderAccess.ReadOnly, cancellationToken);
            var uids = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
            if (uids.Count == 0)
            {
                return new MailboxListing(inbox.UidValidity, []);
            }

            var summaries = await inbox.FetchAsync(uids, new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate), cancellationToken);
            var entries = summaries
                .Select(s => new MailboxEntry(new MailboxKey(inbox.UidValidity, s.UniqueId.Id), (s.InternalDate ?? DateTimeOffset.UtcNow).ToUniversalTime()))
                .ToList();
            return new MailboxListing(inbox.UidValidity, entries);
        }, cancellationToken);

    public Task<InboundEmail?> FetchAsync(MailboxKey key, CancellationToken cancellationToken = default) =>
        RunAsync<InboundEmail?>(async client =>
        {
            var inbox = await OpenInboxAsync(client, FolderAccess.ReadOnly, cancellationToken);
            if (inbox.UidValidity != key.UidValidity)
            {
                return null;
            }

            var uid = new UniqueId(key.UidValidity, key.Uid);
            var summaries = await inbox.FetchAsync([uid], new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate), cancellationToken);
            if (summaries.Count == 0)
            {
                return null;
            }

            MimeMessage message;
            try
            {
                message = await inbox.GetMessageAsync(uid, cancellationToken); // BODY.PEEK: does not set \Seen
            }
            catch (MessageNotFoundException)
            {
                return null;
            }

            return MimeInboundParser.Parse(message, key, summaries[0].InternalDate ?? DateTimeOffset.UtcNow, options.CurrentValue.TrustedAuthServId);
        }, cancellationToken);

    public Task ArchiveAsync(MailboxKey key, CancellationToken cancellationToken = default) =>
        RunAsync(async client =>
        {
            var inbox = await OpenInboxAsync(client, FolderAccess.ReadWrite, cancellationToken);
            if (inbox.UidValidity != key.UidValidity)
            {
                return true; // that UID no longer exists; the message is registered again under its new UID
            }

            var archive = await EnsureFolderAsync(client, options.CurrentValue.ArchiveFolder, cancellationToken);
            await inbox.MoveToAsync([new UniqueId(key.UidValidity, key.Uid)], archive, cancellationToken); // a missing UID is a no-op
            return true;
        }, cancellationToken);

    public Task<bool> SentFolderContainsAsync(string messageId, CancellationToken cancellationToken = default) =>
        RunAsync(async client =>
        {
            var sent = await GetSentFolderAsync(client, cancellationToken);
            await sent.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            var hits = await sent.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), cancellationToken);
            return hits.Count > 0;
        }, cancellationToken);

    public Task PurgeArchiveAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        RunAsync(async client =>
        {
            var archive = await EnsureFolderAsync(client, options.CurrentValue.ArchiveFolder, cancellationToken);
            await archive.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
            var uids = await archive.SearchAsync(SearchQuery.DeliveredBefore(olderThan.UtcDateTime), cancellationToken);
            if (uids.Count > 0)
            {
                await archive.StoreAsync(uids, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true }, cancellationToken);
                await archive.ExpungeAsync(uids, cancellationToken);
                logger.LogInformation("Purged {Count} archived messages", uids.Count);
            }

            return true;
        }, cancellationToken);

    public async Task WaitForChangeAsync(TimeSpan maxWait, CancellationToken cancellationToken = default)
    {
        await _idleLock.WaitAsync(cancellationToken);
        try
        {
            _idleClient = await ConnectAsync(_idleClient, cancellationToken);
            var inbox = await OpenInboxAsync(_idleClient, FolderAccess.ReadOnly, cancellationToken);
            var wait = TimeSpan.FromTicks(Math.Min(maxWait.Ticks, TimeSpan.FromMinutes(agentOptions.CurrentValue.IdleRefreshMinutes).Ticks));

            if (!_idleClient.Capabilities.HasFlag(ImapCapabilities.Idle))
            {
                await Task.Delay(wait, cancellationToken);
                return;
            }

            // IDLE until the message count changes or the wait (re-issued well before Gmail's ~29 minute drop) ends.
            using var done = new CancellationTokenSource(wait);
            void OnChanged(object? sender, EventArgs e) => done.Cancel();
            inbox.CountChanged += OnChanged;
            try
            {
                await _idleClient.IdleAsync(done.Token, cancellationToken);
            }
            finally
            {
                inbox.CountChanged -= OnChanged;
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _idleClient = await DropAsync(_idleClient);
            throw Wrap(ex);
        }
        finally
        {
            _idleLock.Release();
        }
    }

    /// <summary>For hosts that don't save SMTP-sent mail themselves (<see cref="MailboxOptions.AppendToSent"/>).</summary>
    internal Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken) =>
        RunAsync(async client =>
        {
            var sent = await GetSentFolderAsync(client, cancellationToken);
            await sent.AppendAsync(FormatOptions.Default, new AppendRequest(message, MessageFlags.Seen), cancellationToken);
            return true;
        }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _commandClient = await DropAsync(_commandClient);
        _idleClient = await DropAsync(_idleClient);
    }

    private async Task<T> RunAsync<T>(Func<ImapClient, Task<T>> action, CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            _commandClient = await ConnectAsync(_commandClient, cancellationToken);
            return await action(_commandClient);
        }
        catch (Exception ex) when (IsConnectionFailure(ex) && !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _commandClient = await DropAsync(_commandClient); // reconnect on the next call
            throw Wrap(ex);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private async Task<ImapClient> ConnectAsync(ImapClient? client, CancellationToken cancellationToken)
    {
        if (client is { IsConnected: true, IsAuthenticated: true })
        {
            return client;
        }

        await DropAsync(client);
        var o = options.CurrentValue;
        var fresh = new ImapClient();
        try
        {
            await fresh.ConnectAsync(o.Host, o.ImapPort, o.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await credentials.AuthenticateAsync(fresh, cancellationToken);
            return fresh;
        }
        catch
        {
            fresh.Dispose();
            throw;
        }
    }

    private async Task<IMailFolder> OpenInboxAsync(ImapClient client, FolderAccess access, CancellationToken cancellationToken)
    {
        var name = options.CurrentValue.InboxFolder;
        var inbox = name.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? client.Inbox : await client.GetFolderAsync(name, cancellationToken);
        if (!inbox.IsOpen || (access == FolderAccess.ReadWrite && inbox.Access != FolderAccess.ReadWrite))
        {
            await inbox.OpenAsync(access, cancellationToken);
        }

        return inbox;
    }

    private async Task<IMailFolder> GetSentFolderAsync(ImapClient client, CancellationToken cancellationToken)
    {
        var name = options.CurrentValue.SentFolder;
        return string.IsNullOrWhiteSpace(name)
            ? client.GetFolder(SpecialFolder.Sent) ?? throw new InvalidOperationException("The mailbox has no special-use Sent folder; set EmailAgent:Mailbox:SentFolder.")
            : await client.GetFolderAsync(name, cancellationToken);
    }

    /// <summary>Gets the folder, creating each missing level of the path (e.g. the Gmail label "CitadelIQ/Processed").</summary>
    private static async Task<IMailFolder> EnsureFolderAsync(ImapClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFolderAsync(path, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
        }

        var personal = client.PersonalNamespaces[0];
        IMailFolder parent = client.GetFolder(personal);
        foreach (var segment in path.Split(personal.DirectorySeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var children = await parent.GetSubfoldersAsync(false, cancellationToken);
            parent = children.FirstOrDefault(f => f.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                     ?? await parent.CreateAsync(segment, true, cancellationToken)
                     ?? throw new InvalidOperationException("The archive folder could not be created.");
        }

        return parent;
    }

    private static bool IsConnectionFailure(Exception ex) =>
        ex is IOException or SocketException or TimeoutException or System.Security.Authentication.AuthenticationException
            or MailKit.Security.AuthenticationException or SslHandshakeException
            or ServiceNotConnectedException or ServiceNotAuthenticatedException or ImapProtocolException or ProtocolException;

    private static MailboxUnavailableException Wrap(Exception ex) =>
        ex as MailboxUnavailableException ?? new MailboxUnavailableException($"The support mailbox is unavailable ({ex.GetType().Name}).", ex);

    private static async Task<ImapClient?> DropAsync(ImapClient? client)
    {
        if (client is null)
        {
            return null;
        }

        try
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
        catch
        {
            // already broken
        }

        client.Dispose();
        return null;
    }
}
