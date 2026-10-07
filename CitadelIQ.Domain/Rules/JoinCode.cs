using System.Security.Cryptography;
using System.Text;

namespace CitadelIQ.Domain.Rules;

/// <summary>
/// Organization join codes: 12 Crockford base32 characters (60 bits) from a CSPRNG, displayed as XXXX-XXXX-XXXX.
/// Stored normalized (no separators, upper case).
/// </summary>
public static class JoinCode
{
    public const int Length = 12;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }

    /// <summary>Uppercases, drops separators/whitespace and maps the Crockford look-alikes (I/L→1, O→0).
    /// Returns null when the input can't be a join code.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var builder = new StringBuilder(Length);
        foreach (var raw in input)
        {
            if (raw is '-' || char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw) switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                var other => other
            };

            if (!Alphabet.Contains(c) || builder.Length == Length)
            {
                return null;
            }

            builder.Append(c);
        }

        return builder.Length == Length ? builder.ToString() : null;
    }

    public static string Format(string normalized) =>
        normalized.Length == Length ? $"{normalized[..4]}-{normalized[4..8]}-{normalized[8..]}" : normalized;
}
