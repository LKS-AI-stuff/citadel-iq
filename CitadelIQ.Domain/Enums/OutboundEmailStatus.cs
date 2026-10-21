namespace CitadelIQ.Domain.Enums;

public enum OutboundEmailStatus
{
    Pending,
    /// <summary>Handed to SMTP (or about to be). After a crash the Sent folder is checked before any resend.</summary>
    Sending,
    Sent,
    Cancelled,
    Failed
}
