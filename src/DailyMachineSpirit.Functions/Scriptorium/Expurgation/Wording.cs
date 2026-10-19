using System.Text;
using System.Text.RegularExpressions;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>
/// A rite's words, as an Expurgator chooses them: letters and digits, with an apostrophe inside ("don't"). Everything
/// else (spaces, punctuation, backticks, emojis a past expurgation left) stays as it is, between the words.
/// </summary>
public static partial class Wording
{
    public static List<Piece> Split(string text)
    {
        var pieces = new List<Piece>();
        var at = 0;
        foreach (Match word in Word().Matches(text))
        {
            if (word.Index > at)
            {
                pieces.Add(new Piece(text[at..word.Index], false));
            }
            pieces.Add(new Piece(word.Value, true));
            at = word.Index + word.Length;
        }
        if (at < text.Length)
        {
            pieces.Add(new Piece(text[at..], false));
        }
        return pieces;
    }

    /// <summary>The text with the chosen words (by their place among its words, from 0) replaced.</summary>
    public static string Replace(string text, IReadOnlyDictionary<int, string> byWord)
    {
        var replaced = new StringBuilder();
        var word = 0;
        foreach (var piece in Split(text))
        {
            replaced.Append(piece.IsWord && byWord.TryGetValue(word, out var replacement) ? replacement : piece.Text);
            word += piece.IsWord ? 1 : 0;
        }
        return replaced.ToString();
    }

    /// <summary>The text with every word that's one of <paramref name="words"/>' keys, wherever it is, replaced.</summary>
    public static string ReplaceEverywhere(string text, IReadOnlyDictionary<string, string> words)
        => string.Concat(Split(text).Select(piece => piece.IsWord && words.TryGetValue(piece.Text, out var replacement) ? replacement : piece.Text));

    public static int CountWords(string text) => Word().Count(text);

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*")]
    private static partial Regex Word();
}
