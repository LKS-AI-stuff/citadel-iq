namespace CitadelIQ.Application.Common;

/// <summary>An operation that needs a signed-in user ran without one. The Api maps this to a 401.</summary>
public class UnauthenticatedException(string message = "Please sign in.") : Exception(message);
