using MailKit;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>Signs a connected IMAP or SMTP client in to the support mailbox. Credentials are never logged.</summary>
public interface IMailCredentialProvider
{
    Task AuthenticateAsync(IMailService client, CancellationToken cancellationToken);
}

/// <summary>Username + password (a Gmail app password for the test mailbox).</summary>
public sealed class PasswordMailCredentialProvider(IOptionsMonitor<MailboxOptions> options) : IMailCredentialProvider
{
    public Task AuthenticateAsync(IMailService client, CancellationToken cancellationToken) =>
        client.AuthenticateAsync(options.CurrentValue.Username, options.CurrentValue.Password, cancellationToken);
}

/// <summary>Supplies OAuth2 access tokens for the mailbox (Google Workspace service account or a Microsoft 365 Entra
/// app). The production implementation depends on the mailbox host, which is still open (design §13 Q1).</summary>
public interface IMailAccessTokenSource
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>XOAUTH2 with a token from <see cref="IMailAccessTokenSource"/>.</summary>
public sealed class OAuth2MailCredentialProvider(IOptionsMonitor<MailboxOptions> options, IMailAccessTokenSource tokens) : IMailCredentialProvider
{
    public async Task AuthenticateAsync(IMailService client, CancellationToken cancellationToken) =>
        await client.AuthenticateAsync(new SaslMechanismOAuth2(options.CurrentValue.Username, await tokens.GetAccessTokenAsync(cancellationToken)), cancellationToken);
}

/// <summary>Placeholder until a production token source is registered: fails with a clear message.</summary>
public sealed class UnconfiguredMailAccessTokenSource : IMailAccessTokenSource
{
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "EmailAgent:Mailbox:AuthMode is OAuth2 but no OAuth2 token source is registered for the support mailbox.");
}
