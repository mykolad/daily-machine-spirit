using System.Globalization;
using System.Text;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>How the pages turn a rite's words and dates into HTML: everything from a model is encoded here.</summary>
public static class Html
{
    /// <summary>
    /// Escapes the five characters that matter in HTML text and attributes, and nothing else: the pages are UTF-8, so
    /// "·", "’" and "—" stay readable.
    /// </summary>
    public static string Encode(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    /// <summary>
    /// A rite's text with `backticked` spans as <c>code</c>. A backtick left unmatched stays a plain character, so a
    /// stray one never swallows the rest of the text.
    /// </summary>
    public static string WithInlineCode(string text)
    {
        var parts = text.Split('`');
        // Odd parts sit between backticks. With an odd number of backticks the last one has no partner.
        var lastCode = parts.Length % 2 == 1 ? parts.Length - 2 : parts.Length - 3;
        var html = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1 && i <= lastCode)
                html.Append("<code>").Append(Encode(parts[i])).Append("</code>");
            else
                html.Append(i % 2 == 1 ? "`" : string.Empty).Append(Encode(parts[i]));
        }
        return html.ToString();
    }

    /// <summary>The text up to the first sentence end after at least 40 characters (the archive's first line).</summary>
    public static string FirstLine(string text)
    {
        for (var i = 40; i < text.Length; i++)
            if (text[i - 1] is '.' or '!' or '?' && (i == text.Length || char.IsWhiteSpace(text[i])))
                return text[..i];
        return text;
    }

    /// <summary>"9 October 2026".</summary>
    public static string LongDate(DateOnly day) => day.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"9 Oct 2026".</summary>
    public static string ShortDate(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string IsoDate(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
