namespace CitadelIQ.Application.Common;

/// <summary>Thrown when a feature switched off in configuration is requested. The Api maps this to a 503.</summary>
public class FeatureDisabledException(string message) : Exception(message);
