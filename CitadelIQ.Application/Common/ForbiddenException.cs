namespace CitadelIQ.Application.Common;

/// <summary>
/// The caller is signed in and in the right workspace but their role does not allow the action. The Api maps this
/// to a 403 with this message. Items in another workspace are never reported with this — they are a 404.
/// </summary>
public class ForbiddenException(string message) : Exception(message);
