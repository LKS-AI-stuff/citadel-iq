using System.Text;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>
/// Sends plain-text mail from the support address over SMTP (MailKit), with the pre-generated Message-ID and the
/// threading headers. Automatic mail carries <c>Auto-Submitted</c> (RFC 3834); everything carries
/// <c>X-Auto-Response-Suppress: All</c> so Exchange doesn't send out-of-office replies back to us.
/// </summary>
public sealed class SmtpMailSender(
    IOptionsMonitor<MailboxOptions> mailboxOptions,
    IOptionsMonitor<EmailAgentOptions> agentOptions,
    IMailCredentialProvider credentials,
    ImapMailbox mailbox,
    ILogger<SmtpMailSender> logger) : IMailSender
{
    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var mime = Build(message, agentOptions.CurrentValue);
        var o = mailboxOptions.CurrentValue;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(o.SmtpHost, o.SmtpPort, o.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await credentials.AuthenticateAsync(client, cancellationToken);
            await client.SendAsync(mime, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            throw new MailboxUnavailableException($"The mail server is unavailable ({ex.GetType().Name}).", ex);
        }

        try
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Already accepted by the server: a failed QUIT must not turn into a resend.
            logger.LogWarning("SMTP disconnect failed after sending ({ExceptionType})", ex.GetType().Name);
        }

        if (o.AppendToSent)
        {
            try
            {
                await mailbox.AppendToSentAsync(mime, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Already sent: never fail (and so resend) because the copy couldn't be saved. A crash between the send
                // and this append is the one remaining duplicate window on such hosts (design §12).
                logger.LogWarning("Sent mail could not be appended to the Sent folder ({ExceptionType})", ex.GetType().Name);
            }
        }
    }

    public static MimeMessage Build(OutboundMessage message, EmailAgentOptions agent)
    {
        var mime = new MimeMessage
        {
            Subject = message.Subject.ReplaceLineEndings(" "),
            MessageId = message.MessageId,
            Date = DateTimeOffset.UtcNow
        };

        mime.From.Add(new MailboxAddress(agent.SupportDisplayName, agent.SupportAddress));
        mime.To.Add(new MailboxAddress(message.ToDisplayName ?? "", message.To));

        if (!string.IsNullOrWhiteSpace(message.InReplyTo))
        {
            mime.InReplyTo = message.InReplyTo;
        }

        foreach (var reference in message.References)
        {
            mime.References.Add(reference);
        }

        if (message.Automation != OutboundAutomation.None)
        {
            mime.Headers.Add("Auto-Submitted", message.Automation == OutboundAutomation.AutoReplied ? "auto-replied" : "auto-generated");
        }

        mime.Headers.Add("X-Auto-Response-Suppress", "All");

        var body = new TextPart(TextFormat.Plain);
        body.SetText(Encoding.UTF8, message.Body);
        mime.Body = body;
        return mime;
    }
}
