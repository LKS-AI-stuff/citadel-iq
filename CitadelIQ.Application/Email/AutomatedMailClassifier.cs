using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>Loop protection: recognises our own mail and automatically generated mail (out-of-office, bounces,
/// mailing lists), which is never answered.</summary>
public class AutomatedMailClassifier(IOptionsMonitor<EmailAgentOptions> options)
{
    /// <summary>Local part prefix of every Message-ID we generate.</summary>
    public const string MessageIdPrefix = "ciq.";

    private static readonly string[] AutomatedPrecedence = ["bulk", "list", "junk", "auto_reply"];
    private static readonly string[] SystemSenders = ["mailer-daemon", "postmaster"];

    /// <summary>From the support address itself, or carrying one of our own Message-IDs.</summary>
    public bool IsOwnMessage(InboundEmail email)
    {
        var support = options.CurrentValue.SupportAddress;
        if (EmailAddresses.SameAddress(email.FromAddress, support))
        {
            return true;
        }

        var supportDomain = EmailAddresses.DomainOf(support);
        return email.MessageId is { } id && supportDomain is not null
               && id.StartsWith(MessageIdPrefix, StringComparison.OrdinalIgnoreCase)
               && id.EndsWith("@" + supportDomain, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsAutomated(InboundEmail email)
    {
        var a = email.Automation;

        if (!string.IsNullOrWhiteSpace(a.AutoSubmitted) && !a.AutoSubmitted.Trim().StartsWith("no", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (a.Precedence is { } precedence && AutomatedPrecedence.Contains(precedence.Trim().ToLowerInvariant()))
        {
            return true;
        }

        return a.HasListHeaders
               || a.EmptyReturnPath
               || a.IsDeliveryReport
               || a.HasAutoReplyHeader
               || SystemSenders.Contains(EmailAddresses.LocalPartOf(email.FromAddress));
    }
}
