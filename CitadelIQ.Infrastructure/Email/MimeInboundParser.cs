using CitadelIQ.Application.Email;
using CitadelIQ.Application.Interfaces;
using MimeKit;
using MimeKit.Cryptography;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>Turns a MimeKit message into the provider-neutral <see cref="InboundEmail"/>.</summary>
public static class MimeInboundParser
{
    private static readonly string[] AutoReplyHeaders = ["X-Autoreply", "X-Autorespond", "X-AutoReply-From", "X-Mail-Autoreply"];

    public static InboundEmail Parse(MimeMessage message, MailboxKey key, DateTimeOffset receivedAtUtc, string trustedAuthServId)
    {
        var authResults = TrustedAuthResults(message.Headers, trustedAuthServId);
        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            body = HtmlToText.Convert(message.HtmlBody);
        }

        return new InboundEmail(
            key,
            receivedAtUtc.ToUniversalTime(),
            message.MessageId,
            message.MessageId is not null && IsMessageIdSigned(message.Headers, authResults),
            message.InReplyTo,
            message.References.ToList(),
            message.From.Mailboxes.FirstOrDefault()?.Address?.Trim() ?? "",
            message.Subject?.Trim() ?? "",
            body ?? "",
            authResults,
            Automation(message));
    }

    /// <summary>
    /// Results from the topmost <c>Authentication-Results</c> header whose authserv-id is the provider's own receiving
    /// server. Anything the sender (or an earlier hop) added — even claiming the same authserv-id further down — is
    /// never consulted.
    /// </summary>
    public static IReadOnlyList<AuthResult> TrustedAuthResults(HeaderList headers, string trustedAuthServId)
    {
        if (string.IsNullOrWhiteSpace(trustedAuthServId))
        {
            return [];
        }

        foreach (var header in headers) // in message order: the topmost (most recently added) first
        {
            if (!header.Field.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase)
                || !AuthenticationResults.TryParse(header.RawValue, out var parsed)
                || !string.Equals(parsed.AuthenticationServiceIdentifier, trustedAuthServId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var results = new List<AuthResult>();
            foreach (var r in parsed.Results)
            {
                var method = r.Method?.Trim().ToLowerInvariant();
                var result = r.Result?.Trim().ToLowerInvariant() ?? "";
                if (method == "spf")
                {
                    results.Add(new AuthResult(method, result, EmailAddresses.DomainOf(PropertyValue(r, "smtp", "mailfrom"))));
                }
                else if (method == "dkim")
                {
                    if (PropertyValue(r, "header", "d") is { } d) results.Add(new AuthResult(method, result, EmailAddresses.DomainOf(d)));
                    if (PropertyValue(r, "header", "i") is { } i) results.Add(new AuthResult(method, result, EmailAddresses.DomainOf(i)));
                }
            }

            return results;
        }

        return [];
    }

    /// <summary>True when a DKIM signature from a passing, trusted domain covers the Message-ID header (<c>h=</c>).</summary>
    public static bool IsMessageIdSigned(HeaderList headers, IReadOnlyList<AuthResult> trustedResults)
    {
        var passingDomains = trustedResults
            .Where(r => r.Method == "dkim" && r.Result == "pass" && r.Domain is not null)
            .Select(r => r.Domain!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (passingDomains.Count == 0)
        {
            return false;
        }

        foreach (var header in headers)
        {
            if (!header.Field.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tags = ParseTags(header.Value);
            if (tags.TryGetValue("d", out var d) && passingDomains.Contains(d)
                && tags.TryGetValue("h", out var h)
                && h.Split(':').Any(name => name.Trim().Equals("message-id", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static AutomationSignals Automation(MimeMessage message)
    {
        var headers = message.Headers;
        var returnPath = headers[HeaderId.ReturnPath];
        return new AutomationSignals(
            headers["Auto-Submitted"],
            headers[HeaderId.Precedence],
            headers.Contains("List-Id") || headers.Contains("List-Unsubscribe"),
            returnPath is not null && returnPath.Trim() == "<>",
            message.Body is MultipartReport || message.Body?.ContentType.IsMimeType("multipart", "report") == true,
            AutoReplyHeaders.Any(headers.Contains));
    }

    private static string? PropertyValue(AuthenticationMethodResult result, string type, string property) =>
        result.Properties
            .FirstOrDefault(p => p.PropertyType.Equals(type, StringComparison.OrdinalIgnoreCase) && p.Property.Equals(property, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static Dictionary<string, string> ParseTags(string value)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var name = part[..eq].Trim();
            var tagValue = string.Concat(part[(eq + 1)..].Where(c => !char.IsWhiteSpace(c)));
            tags.TryAdd(name, tagValue);
        }

        return tags;
    }
}
