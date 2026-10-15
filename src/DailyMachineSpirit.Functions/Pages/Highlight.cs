using System.Text;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Text as HTML with every occurrence of the searched words in <c>&lt;mark&gt;</c>, ignoring case.</summary>
public static class Highlight
{
    public static string Of(string text, IReadOnlyList<string> words)
    {
        var html = new StringBuilder();
        var at = 0;
        foreach (var (start, end) in Merged(words.SelectMany(word => Occurrences(text, word))))
        {
            html.Append(Html.Encode(text[at..start])).Append("<mark>").Append(Html.Encode(text[start..end])).Append("</mark>");
            at = end;
        }
        return html.Append(Html.Encode(text[at..])).ToString();
    }

    private static IEnumerable<(int Start, int End)> Occurrences(string text, string word)
    {
        for (var start = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); start >= 0;
             start = text.IndexOf(word, start + word.Length, StringComparison.OrdinalIgnoreCase))
        {
            yield return (start, start + word.Length);
        }
    }

    // Words can overlap or touch ("cache" in "cached", "re" and "run" in "rerun"): one mark covers them, not two nested.
    private static List<(int Start, int End)> Merged(IEnumerable<(int Start, int End)> ranges)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges.OrderBy(range => range.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged;
    }
}
