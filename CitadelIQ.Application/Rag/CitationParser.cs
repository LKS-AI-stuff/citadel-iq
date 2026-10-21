using System.Text.RegularExpressions;

namespace CitadelIQ.Application.Rag;

/// <summary>Finds <c>[n]</c> / <c>[1, 2]</c> citation markers in an answer.</summary>
public static partial class CitationParser
{
    [GeneratedRegex(@"\[(\d{1,3}(?:\s*,\s*\d{1,3})*)\]")]
    private static partial Regex Marker();

    [GeneratedRegex(@"[ \t]*\[\d{1,3}(?:\s*,\s*\d{1,3})*\]")]
    private static partial Regex MarkerWithLeadingSpace();

    /// <summary>Distinct valid source numbers (1..<paramref name="sourceCount"/>) in first-seen order.</summary>
    public static IReadOnlyList<int> Extract(string text, int sourceCount)
    {
        var cited = new List<int>();
        foreach (Match match in Marker().Matches(text))
        {
            foreach (var part in match.Groups[1].Value.Split(','))
            {
                if (int.TryParse(part.Trim(), out var number) && number >= 1 && number <= sourceCount && !cited.Contains(number))
                {
                    cited.Add(number);
                }
            }
        }

        return cited;
    }

    /// <summary>Removes citation markers (and the space before them) — used on past answers before they enter prompts.</summary>
    public static string StripMarkers(string text) => MarkerWithLeadingSpace().Replace(text, string.Empty);

    /// <summary>Drops numbers outside 1..<paramref name="sourceCount"/> from each marker, and the whole marker (with
    /// the space before it) when none is valid — what a plain-text email reply shows instead of the UI's chips.</summary>
    public static string RemoveInvalid(string text, int sourceCount) =>
        MarkerWithLeadingSpace().Replace(text, match =>
        {
            var valid = match.Value.Trim().TrimStart('[').TrimEnd(']').Split(',')
                .Select(part => int.TryParse(part.Trim(), out var n) ? n : 0)
                .Where(n => n >= 1 && n <= sourceCount)
                .Distinct()
                .ToList();

            if (valid.Count == 0)
            {
                return string.Empty;
            }

            var leading = match.Value[..(match.Value.Length - match.Value.TrimStart().Length)];
            return leading + "[" + string.Join(", ", valid) + "]";
        });
}
