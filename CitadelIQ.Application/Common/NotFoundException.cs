namespace CitadelIQ.Application.Common;

/// <summary>
/// Thrown when a requested resource does not exist. The Api layer maps this to a 404 with the
/// message shown here — keep messages safe to display to a user.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
