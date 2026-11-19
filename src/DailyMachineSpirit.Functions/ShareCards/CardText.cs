using System.Text;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>
/// A rite's text as a share card sets it: runs of characters, never markup, the only rule a pair of backticks, which
/// becomes a code chip. A backtick without its pair is drawn as it is.
/// </summary>
public static class CardText
{
    /// <summary>
    /// The text in words, as the card breaks it into lines: whitespace separates them, and a word is every run between
    /// two spaces, so code stays with the punctuation after it ("<c>`x`,</c>") and is never broken, even when it holds
    /// a space.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<CardRun>> Words(string text)
    {
        var words = new List<IReadOnlyList<CardRun>>();
        var word = new List<CardRun>();
        foreach (var run in Runs(text))
        {
            if (run.IsCode)
            {
                word.Add(run);
                continue;
            }
            var parts = run.Text.Split((char[]?)null);
            for (var index = 0; index < parts.Length; index++)
            {
                // Whitespace between this part and the one before ends the word so far.
                if (index > 0 && word.Count > 0)
                {
                    words.Add(word);
                    word = [];
                }
                if (parts[index].Length > 0)
                {
                    word.Add(new CardRun(parts[index], false));
                }
            }
        }
        if (word.Count > 0)
        {
            words.Add(word);
        }
        return words;
    }

    /// <summary>The text for a screen reader (the image's alt text): code without its backticks.</summary>
    public static string Plain(string text) => string.Concat(Runs(text).Select(run => run.Text));

    // Plain text and code, in order. A backtick opens code only when another one closes it.
    private static List<CardRun> Runs(string text)
    {
        var runs = new List<CardRun>();
        var plain = new StringBuilder();
        var at = 0;
        while (at < text.Length)
        {
            var open = text.IndexOf('`', at);
            var close = open < 0 ? -1 : text.IndexOf('`', open + 1);
            if (close < 0)
            {
                plain.Append(text, at, text.Length - at);
                break;
            }
            plain.Append(text, at, open - at);
            // Two backticks with nothing between hold no code: they're drawn as they are.
            if (close == open + 1)
            {
                plain.Append("``");
            }
            else
            {
                if (plain.Length > 0)
                {
                    runs.Add(new CardRun(plain.ToString(), false));
                    plain.Clear();
                }
                runs.Add(new CardRun(text[(open + 1)..close], true));
            }
            at = close + 1;
        }
        if (plain.Length > 0)
        {
            runs.Add(new CardRun(plain.ToString(), false));
        }
        return runs;
    }
}
