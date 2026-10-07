using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;

namespace CitadelIQ.Api.Authentication;

/// <summary>
/// The minimal principal kept in the session cookie: identity provider issuer + subject (the user's key), name and
/// email for display, and the original sign-in time for the absolute session lifetime. No roles or workspace — those
/// are read from the database on every request.
/// </summary>
public static class SessionClaims
{
    public const string Issuer = "iss";
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string SignedInAt = "citadeliq:signed_in_at";
    public const string AuthenticationType = "CitadelIQ";

    public static ClaimsPrincipal Create(string issuer, string subject, string displayName, string email) =>
        new(new ClaimsIdentity(
            [
                new Claim(Issuer, issuer),
                new Claim(Subject, subject),
                new Claim(Name, displayName),
                new Claim(Email, email),
                new Claim(SignedInAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            ],
            AuthenticationType,
            Name,
            roleType: null));

    public static bool TryGetIdentity(ClaimsPrincipal principal, [NotNullWhen(true)] out string? issuer, [NotNullWhen(true)] out string? subject)
    {
        var sub = principal.FindFirst(Subject);
        subject = sub?.Value;
        // Some token handlers surface the issuer only on the claim, not as an "iss" claim.
        issuer = principal.FindFirst(Issuer)?.Value ?? sub?.Issuer;
        return !string.IsNullOrEmpty(issuer) && !string.IsNullOrEmpty(subject);
    }

    public static DateTimeOffset? GetSignedInAt(ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirst(SignedInAt)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
