namespace CitadelIQ.Domain.Entities;

internal static class EmailText
{
    public static string Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    public static string? TruncateOrNull(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value, maxLength);
}
