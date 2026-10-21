using System.Text;
using System.Text.RegularExpressions;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Email;

/// <summary>
/// Keeps only what the sender wrote in this message: drops quoted history ("&gt;" lines, "On … wrote:", Outlook's
/// From:/Sent: block, "-----Original Message-----"), the signature ("-- ") and mobile footers, collapses blank lines
/// and caps the length. Best effort — quoted text is never used as history anyway (requirements decision 8).
/// </summary>
public partial class EmailBodyCleaner(IOptionsMonitor<EmailAgentOptions> options)
{
    [GeneratedRegex(@"^\s*On\b.{0,300}\bwrote:\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OnWroteLine();

    [GeneratedRegex(@"\bwrote:\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex EndsWithWrote();

    [GeneratedRegex(@"^\s*-{2,}\s*Original Message\s*-{2,}\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OriginalMessage();

    [GeneratedRegex(@"^\s*_{10,}\s*$")]
    private static partial Regex Underscores();

    [GeneratedRegex(@"^\s*\*?From:\*?\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex FromHeader();

    [GeneratedRegex(@"^\s*\*?(Sent|Date|To|Subject):\*?\s", RegexOptions.IgnoreCase)]
    private static partial Regex OutlookHeader();

    [GeneratedRegex(@"^\s*Sent from my \w+", RegexOptions.IgnoreCase)]
    private static partial Regex MobileFooter();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBlankLines();

    public string Clean(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "";
        }

        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line == "-- " || line.TrimEnd() == "--" || OriginalMessage().IsMatch(line) || OnWroteLine().IsMatch(line)
                || IsWrappedOnWrote(lines, i) || IsOutlookBlock(lines, i) || MobileFooter().IsMatch(line))
            {
                break; // everything below is signature or quoted history
            }

            if (line.TrimStart().StartsWith('>'))
            {
                continue;
            }

            kept.Append(line.TrimEnd()).Append('\n');
        }

        var text = ManyBlankLines().Replace(TrailingSpaces().Replace(kept.ToString(), "\n"), "\n\n").Trim();
        var max = options.CurrentValue.MaxBodyChars;
        return text.Length > max ? text[..max] : text;
    }

    /// <summary>Gmail wraps a long attribution: "On Mon, 1 Jan 2026 at 10:00, Jane Doe &lt;jane@…&gt;" / "wrote:".</summary>
    private static bool IsWrappedOnWrote(string[] lines, int i) =>
        lines[i].TrimStart().StartsWith("On ", StringComparison.OrdinalIgnoreCase)
        && i + 1 < lines.Length
        && EndsWithWrote().IsMatch(lines[i + 1])
        && lines[i + 1].Length < 200;

    /// <summary>Outlook: an optional "____" rule, then "From: …" followed within a few lines by Sent:/Date:/To:/Subject:.</summary>
    private static bool IsOutlookBlock(string[] lines, int i)
    {
        var start = Underscores().IsMatch(lines[i]) ? i + 1 : i;
        while (start < lines.Length && start <= i + 2 && string.IsNullOrWhiteSpace(lines[start]))
        {
            start++;
        }

        if (start >= lines.Length || !FromHeader().IsMatch(lines[start]))
        {
            return false;
        }

        for (var j = start + 1; j < Math.Min(lines.Length, start + 5); j++)
        {
            if (OutlookHeader().IsMatch(lines[j]))
            {
                return true;
            }
        }

        return false;
    }
}
