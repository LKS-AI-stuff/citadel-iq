namespace CitadelIQ.Api.Contracts;

public record SendDraftRequest(string? Text);

/// <summary>Nullable so a body without the flag is a 400, not a silent opt-out.</summary>
public record NotificationPreferenceRequest(bool? EmailOnNewDrafts);
