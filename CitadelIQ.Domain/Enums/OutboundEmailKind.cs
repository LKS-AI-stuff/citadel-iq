namespace CitadelIQ.Domain.Enums;

public enum OutboundEmailKind
{
    AutoReply,
    HoldingReply,
    NoConfidentReply,
    /// <summary>A draft an Owner/Admin reviewed and sent.</summary>
    DraftReply,
    /// <summary>The batched "drafts awaiting review" email to an Owner/Admin.</summary>
    DraftNotification
}
