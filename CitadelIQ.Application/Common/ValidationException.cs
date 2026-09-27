namespace CitadelIQ.Application.Common;

/// <summary>
/// Thrown for request-level validation failures that aren't domain invariants (e.g. an unknown
/// enum value, a missing required field). The Api layer maps this to a 400 with the message shown
/// here — keep messages safe to display to a user.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
