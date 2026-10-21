namespace CitadelIQ.Application.Email;

public static class EmailAddresses
{
    /// <summary>Lower-cased domain after the last '@', or null.</summary>
    public static string? DomainOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var at = address.LastIndexOf('@');
        var domain = at >= 0 ? address[(at + 1)..] : address;
        domain = domain.Trim().Trim('>', '.').ToLowerInvariant();
        return domain.Length == 0 ? null : domain;
    }

    /// <summary>Lower-cased part before the last '@', or the whole value.</summary>
    public static string LocalPartOf(string address)
    {
        var at = address.LastIndexOf('@');
        return (at >= 0 ? address[..at] : address).Trim().ToLowerInvariant();
    }

    public static bool SameAddress(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>SHA-256 (hex) of a Message-ID, for the secondary duplicate check — the id itself is not stored.</summary>
    public static string HashMessageId(string messageId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(messageId.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes);
    }
}
