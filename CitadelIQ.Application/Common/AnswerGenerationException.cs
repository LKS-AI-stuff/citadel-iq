namespace CitadelIQ.Application.Common;

/// <summary>The language model could not produce a response. The message is safe to show to a user (no provider
/// details). The Api maps this to a 502, or to an <c>error</c> event once streaming has started.</summary>
public class AnswerGenerationException(string message, Exception? inner = null) : Exception(message, inner);
