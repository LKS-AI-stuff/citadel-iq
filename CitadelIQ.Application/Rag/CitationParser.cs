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
}
