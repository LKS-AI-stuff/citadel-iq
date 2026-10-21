using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Options;

public class EmailAgentOptionsValidator : IValidateOptions<EmailAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailAgentOptions o)
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(o.Mode)) errors.Add("EmailAgent:Mode must be Off, DraftsOnly or On.");

        if (o.Mode != EmailAgentMode.Off)
        {
            if (!LooksLikeAddress(o.SupportAddress)) errors.Add("EmailAgent:SupportAddress must be an email address when the agent is on.");
            if (!Uri.TryCreate(o.PortalBaseUrl, UriKind.Absolute, out var portal) || portal.Scheme is not ("https" or "http"))
                errors.Add("EmailAgent:PortalBaseUrl must be an absolute http(s) URL when the agent is on.");
        }

        if (o.ConfidenceThreshold is < 0 or > 1) errors.Add("EmailAgent:ConfidenceThreshold must be between 0 and 1.");
        if (o.MinCitedSimilarity is < 0 or > 1) errors.Add("EmailAgent:MinCitedSimilarity must be between 0 and 1.");
        if (o.MaxSearchAttempts is < 1 or > 2) errors.Add("EmailAgent:MaxSearchAttempts must be 1 or 2.");
        if (o.MaxBodyChars <= 0) errors.Add("EmailAgent:MaxBodyChars must be positive.");
        if (o.MaxAutomaticRepliesPerThread <= 0) errors.Add("EmailAgent:MaxAutomaticRepliesPerThread must be positive.");
        if (o.MaxDraftChars <= 0) errors.Add("EmailAgent:MaxDraftChars must be positive.");
        if (o.Limits.PerUserPerHour <= 0 || o.Limits.PerOrganizationPerDay <= 0 || o.Limits.TotalPerDay <= 0 || o.Limits.MaxSentPerDay <= 0)
            errors.Add("EmailAgent:Limits values must be positive.");
        if (o.MaxAttempts <= 0) errors.Add("EmailAgent:MaxAttempts must be positive.");
        if (o.RetryDelaysSeconds.Any(d => d < 0)) errors.Add("EmailAgent:RetryDelaysSeconds must not be negative.");
        if (o.ProcessingLeaseMinutes <= 0) errors.Add("EmailAgent:ProcessingLeaseMinutes must be positive.");
        if (o.PollIntervalSeconds <= 0) errors.Add("EmailAgent:PollIntervalSeconds must be positive.");
        if (o.IdleRefreshMinutes <= 0) errors.Add("EmailAgent:IdleRefreshMinutes must be positive.");
        if (o.NotificationIntervalMinutes <= 0) errors.Add("EmailAgent:NotificationIntervalMinutes must be positive.");
        if (o.DraftExpiryDays <= 0) errors.Add("EmailAgent:DraftExpiryDays must be positive.");
        if (o.ActivityRetentionDays <= 0) errors.Add("EmailAgent:ActivityRetentionDays must be positive.");
        if (o.MaintenanceIntervalMinutes <= 0) errors.Add("EmailAgent:MaintenanceIntervalMinutes must be positive.");
        if (o.HealthFailureMinutes <= 0) errors.Add("EmailAgent:HealthFailureMinutes must be positive.");
        if (string.IsNullOrWhiteSpace(o.Texts.NotificationMarker)) errors.Add("EmailAgent:Texts:NotificationMarker is required.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool LooksLikeAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.IndexOf('@') is > 0 && value.IndexOf('@') == value.LastIndexOf('@') && !value.EndsWith('@');
}
