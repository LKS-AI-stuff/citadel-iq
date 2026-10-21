namespace CitadelIQ.Application.Common;

/// <summary>The support mailbox (IMAP) or the mail server (SMTP) could not be reached or refused the operation.
/// Always treated as transient: the email is retried, never lost. Never shown to users.</summary>
public class MailboxUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
