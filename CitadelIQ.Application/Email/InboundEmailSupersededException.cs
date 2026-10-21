namespace CitadelIQ.Application.Email;

/// <summary>The claimed processing row was superseded by a UIDVALIDITY change while it was being handled. Thrown inside
/// the commit so the whole outcome rolls back; the message is processed once, under its new mailbox key.</summary>
public class InboundEmailSupersededException() : Exception("The inbound email was superseded while it was being processed.");
