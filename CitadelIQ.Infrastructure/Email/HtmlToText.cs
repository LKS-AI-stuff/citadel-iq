using System.Net;
using System.Text.RegularExpressions;

namespace CitadelIQ.Infrastructure.Email;

/// <summary>A deliberately small HTML-to-text conversion for HTML-only mail: drops scripts, styles and quoted blocks,
/// turns block ends into line breaks, strips tags and decodes entities.</summary>
public static partial class HtmlToText
{
    [GeneratedRegex(@"<(script|style|head)\b.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Invisible();

    [GeneratedRegex(@"<blockquote\b.*?</blockquote\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Quotes();

    [GeneratedRegex(@"<(br|/p|/div|/li|/tr|/h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n\s*\n\s*\n+")]
    private static partial Regex BlankLines();

    public static string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = Invisible().Replace(html, " ");
        text = Quotes().Replace(text, "\n");
        text = LineBreaks().Replace(text, "\n");
        text = Tags().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace("\r", "");
        text = Spaces().Replace(text, " ");
        text = string.Join('\n', text.Split('\n').Select(l => l.Trim()));
        return BlankLines().Replace(text, "\n\n").Trim();
    }
}
