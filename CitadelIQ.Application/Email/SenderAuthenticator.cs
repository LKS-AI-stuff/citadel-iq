using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Application.Email;

/// <summary>
/// Accepts a sender when SPF (<c>smtp.mailfrom</c>) or DKIM (<c>header.d</c>/<c>header.i</c>) passed for a domain
/// aligned with the From domain, as recorded by the mailbox provider's own receiving server (requirements decisions
/// 9 and 16 — DMARC's property, without requiring a DMARC record).
/// </summary>
public static class SenderAuthenticator
{
    public static bool IsAuthenticated(string fromAddress, IEnumerable<AuthResult> trustedResults)
    {
        var fromDomain = EmailAddresses.DomainOf(fromAddress);
        return fromDomain is not null && trustedResults.Any(r =>
            r.Method.Trim().ToLowerInvariant() is "spf" or "dkim"
            && string.Equals(r.Result.Trim(), "pass", StringComparison.OrdinalIgnoreCase)
            && IsAligned(fromDomain, EmailAddresses.DomainOf(r.Domain)));
    }

    /// <summary>
    /// Relaxed alignment without a public-suffix list: equal, or one is a subdomain of the other. Nobody can obtain an
    /// SPF or DKIM pass for a public suffix such as <c>co.uk</c>, so this approximates DMARC's organizational-domain
    /// rule; a PSL-based check can replace it here.
    /// </summary>
    public static bool IsAligned(string fromDomain, string? authenticatedDomain)
    {
        if (string.IsNullOrWhiteSpace(authenticatedDomain))
        {
            return false;
        }

        var a = fromDomain.Trim().TrimEnd('.').ToLowerInvariant();
        var b = authenticatedDomain.Trim().TrimEnd('.').ToLowerInvariant();
        return a == b || a.EndsWith("." + b, StringComparison.Ordinal) || b.EndsWith("." + a, StringComparison.Ordinal);
    }
}
