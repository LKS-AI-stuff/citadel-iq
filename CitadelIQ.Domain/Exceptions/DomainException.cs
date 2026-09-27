namespace CitadelIQ.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated. The Api layer maps these to safe,
/// user-friendly error responses — the message text here is always safe to show a user.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
