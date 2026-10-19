using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>A word an Expurgator chose to erase: its part, its place among that part's words (from 0), and what replaces it.</summary>
public sealed record RedactedWord(RitePart Part, int Word, string Emoji)
{
    /// <summary>The word, as a form carries it: <c>text:12</c>.</summary>
    public static string Chosen(RitePart part, int word) => $"{part.Key()}:{word}";

    /// <summary>A chosen word from a form: <c>text:12</c>.</summary>
    public static Option<(RitePart Part, int Word)> ParseChosen(string chosen)
    {
        var halves = chosen.Split(':');
        return halves.Length == 2 && int.TryParse(halves[1], out var word) && word >= 0
            ? RiteParts.All.Where(part => part.Key() == halves[0]).HeadOrNone().Map(part => (part, word))
            : None;
    }

    /// <summary>As the confirming form carries it: <c>text:12:💀</c>.</summary>
    public override string ToString() => $"{Chosen(Part, Word)}:{Emoji}";

    /// <summary>One from the confirming form, if it's well formed and its emoji is one of <see cref="LostToTime"/>'s.</summary>
    public static Option<RedactedWord> Parse(string carried)
    {
        var last = carried.LastIndexOf(':');
        return last < 0
            ? None
            : ParseChosen(carried[..last])
                .Filter(_ => LostToTime.IsOne(carried[(last + 1)..]))
                .Map(chosen => new RedactedWord(chosen.Part, chosen.Word, carried[(last + 1)..]));
    }
}
