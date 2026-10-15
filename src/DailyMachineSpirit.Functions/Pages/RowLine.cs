using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The line of text under a title in the Archive's rows: the rite's opening words, or for a search, the words around
/// the first match, so a reader sees why the rite was found. Plain text: backticks are dropped, since a cut could
/// split a code span.
/// </summary>
public static class RowLine
{
    private const int FirstLineLength = 84;
    // A match this close to the start shows in the opening words, a little longer than usual to fit it.
    private const int NearStart = 40;
    private const int NearStartLength = 90;
    private const int Before = 30;
    private const int After = 50;

    /// <summary>The opening words, cut at the last word before 84 characters.</summary>
    public static string Opening(Rite rite) => Clip(Plain(rite.Writing.Text), FirstLineLength);

    /// <summary>
    /// Around the first word found in the text; else the opening words when the title holds the words, and the words
    /// around the first one in the Heretical Truth when only it does (then <c>FromTruth</c>).
    /// </summary>
    public static (string Line, bool FromTruth) For(Rite rite, IReadOnlyList<string> words)
    {
        var text = Plain(rite.Writing.Text);
        var inText = FirstMatch(text, words);
        if (inText.IsSome || FirstMatch(rite.Writing.Title, words).IsSome)
        {
            return (inText.Match(Some: match => Around(text, match), None: () => Clip(text, FirstLineLength)), false);
        }

        var truth = Plain(rite.Writing.HereticalTruth);
        return FirstMatch(truth, words).Match(
            Some: match => (Around(truth, match), true),
            None: () => (Clip(text, FirstLineLength), false));
    }

    private static Option<(int Start, int Length)> FirstMatch(string text, IReadOnlyList<string> words)
        => words
            .Select(word => (Start: text.IndexOf(word, StringComparison.OrdinalIgnoreCase), word.Length))
            .Where(match => match.Start >= 0)
            .OrderBy(match => match.Start)
            .HeadOrNone();

    private static string Plain(string text) => text.Replace("`", string.Empty);

    private static string Around(string text, (int Start, int Length) match)
    {
        if (match.Start < NearStart)
        {
            return Clip(text, NearStartLength);
        }

        // From the first word that starts after 30 characters before the match, to the last word that ends before 50
        // after it.
        var space = text.IndexOf(' ', match.Start - Before);
        var start = space < 0 || space >= match.Start ? match.Start : space + 1;
        var wanted = match.Start + match.Length + After;
        var end = wanted >= text.Length ? text.Length : Math.Max(text.LastIndexOf(' ', wanted), match.Start + match.Length);
        var cut = TrimEndPunctuation(text[start..end]);
        return $"{(start > 0 ? "…" : string.Empty)}{cut}{(end < text.Length ? "…" : string.Empty)}";
    }

    // The text up to the last whole word before the length, without the punctuation it ended on, then "…".
    private static string Clip(string text, int length)
    {
        if (text.Length <= length)
        {
            return text;
        }

        var space = text.LastIndexOf(' ', length);
        return $"{TrimEndPunctuation(text[..(space > 0 ? space : length)])}…";
    }

    private static string TrimEndPunctuation(string text) => text.TrimEnd().TrimEnd(',', '.', ';', ':', '!', '?', '—', '–', '-').TrimEnd();
}
