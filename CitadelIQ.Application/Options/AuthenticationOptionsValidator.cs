using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Options;

public class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthenticationOptions o)
    {
        var errors = new List<string>();

        if (o.Mode is not (AuthenticationOptions.OidcMode or AuthenticationOptions.DevelopmentLoginMode))
        {
            errors.Add($"Authentication:Mode must be '{AuthenticationOptions.OidcMode}' or '{AuthenticationOptions.DevelopmentLoginMode}'.");
        }

        if (o.Mode == AuthenticationOptions.OidcMode)
        {
            if (string.IsNullOrWhiteSpace(o.Oidc.Authority)) errors.Add("Authentication:Oidc:Authority is required.");
            if (string.IsNullOrWhiteSpace(o.Oidc.ClientId)) errors.Add("Authentication:Oidc:ClientId is required.");
            if (string.IsNullOrWhiteSpace(o.Oidc.ClientSecret)) errors.Add("Authentication:Oidc:ClientSecret is required (user-secrets or environment variable).");
        }

        if (string.IsNullOrWhiteSpace(o.Session.CookieName)) errors.Add("Authentication:Session:CookieName is required.");
        if (o.Session.IdleTimeoutHours <= 0) errors.Add("Authentication:Session:IdleTimeoutHours must be positive.");
        if (o.Session.AbsoluteLifetimeDays <= 0) errors.Add("Authentication:Session:AbsoluteLifetimeDays must be positive.");
        if (o.JoinRateLimit.PermitLimit <= 0) errors.Add("Authentication:JoinRateLimit:PermitLimit must be positive.");
        if (o.JoinRateLimit.WindowMinutes <= 0) errors.Add("Authentication:JoinRateLimit:WindowMinutes must be positive.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
