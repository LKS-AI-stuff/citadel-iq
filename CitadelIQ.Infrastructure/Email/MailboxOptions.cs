using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>
/// Support mailbox connection (<c>EmailAgent:Mailbox</c>), Infrastructure-only. An empty <see cref="Host"/> means the
/// agent is not wired at all (local development and tests). <see cref="Password"/> is a secret: user-secrets or
/// <c>EmailAgent__Mailbox__Password</c> only.
/// </summary>
public class MailboxOptions
{
    public const string PasswordAuth = "Password";
    public const string OAuth2Auth = "OAuth2";

    /// <summary>IMAP host, e.g. <c>imap.gmail.com</c>.</summary>
    public string Host { get; set; } = "";

    public int ImapPort { get; set; } = 993;

    public string SmtpHost { get; set; } = "";

    public int SmtpPort { get; set; } = 587;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary><c>Password</c> (Gmail app password — testing) or <c>OAuth2</c> (production domain mailbox).</summary>
    public string AuthMode { get; set; } = PasswordAuth;

    public string InboxFolder { get; set; } = "INBOX";

    public string ArchiveFolder { get; set; } = "CitadelIQ/Processed";

    /// <summary>Empty ⇒ the server's special-use \Sent folder.</summary>
    public string SentFolder { get; set; } = "[Gmail]/Sent Mail";

    /// <summary>True for hosts that don't save SMTP-sent mail to Sent themselves (Gmail and Google Workspace do).</summary>
    public bool AppendToSent { get; set; }

    /// <summary>The authserv-id of the provider's own receiving server, e.g. <c>mx.google.com</c>. Only its
    /// <c>Authentication-Results</c> header is trusted.</summary>
    public string TrustedAuthServId { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

public class MailboxOptionsValidator : IValidateOptions<MailboxOptions>
{
    public ValidateOptionsResult Validate(string? name, MailboxOptions o)
    {
        if (!o.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(o.SmtpHost)) errors.Add("EmailAgent:Mailbox:SmtpHost is required when Host is set.");
        if (string.IsNullOrWhiteSpace(o.Username)) errors.Add("EmailAgent:Mailbox:Username is required when Host is set.");
        if (o.AuthMode is not (MailboxOptions.PasswordAuth or MailboxOptions.OAuth2Auth)) errors.Add("EmailAgent:Mailbox:AuthMode must be Password or OAuth2.");
        if (o.AuthMode == MailboxOptions.PasswordAuth && string.IsNullOrWhiteSpace(o.Password))
            errors.Add("EmailAgent:Mailbox:Password is required for Password authentication (user-secrets or environment variable).");
        if (o.ImapPort is <= 0 or > 65535 || o.SmtpPort is <= 0 or > 65535) errors.Add("EmailAgent:Mailbox ports must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(o.InboxFolder) || string.IsNullOrWhiteSpace(o.ArchiveFolder)) errors.Add("EmailAgent:Mailbox:InboxFolder and ArchiveFolder are required.");
        if (string.Equals(o.InboxFolder, o.ArchiveFolder, StringComparison.OrdinalIgnoreCase)) errors.Add("EmailAgent:Mailbox:ArchiveFolder must differ from InboxFolder.");
        if (string.IsNullOrWhiteSpace(o.TrustedAuthServId)) errors.Add("EmailAgent:Mailbox:TrustedAuthServId is required (e.g. mx.google.com).");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
