using System.Text.RegularExpressions;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// The images the recent rites lean on (#193): one rite a day, a gate or a seal is a voice; twenty in the Archive, it's
/// all a reader sees. Counted from the words, so the writer can be asked to rest them and reach for others.
/// </summary>
public static class WornImages
{
    // What the first rites leaned on most, and their near kin, counted with or without a plural "s".
    private static readonly IReadOnlyList<string> Watched =
        ["gate", "seal", "green", "bell", "door", "key", "flame", "candle", "altar", "incense", "offering", "threshold", "vessel", "lantern", "ask not"];

    /// <summary>Images that would rest well instead: none of them is watched for wear.</summary>
    public static IReadOnlyList<string> Fresh { get; } = ["forges", "ledgers", "lamps", "wells", "looms", "tides", "roads", "anvils", "orchards", "clocks"];

    /// <summary>
    /// The watched images in at least <paramref name="atLeast"/> of the texts, the most used first. Each text counts
    /// once: a litany's refrain repeats its image, and that's one rite leaning on it, not a habit.
    /// </summary>
    public static List<string> In(IEnumerable<string> texts, int atLeast)
    {
        var all = texts.ToList();
        return Watched
            .Select(image => (Image: image, Count: all.Count(text => Regex.IsMatch(text, $@"\b{Regex.Escape(image)}s?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))))
            .Where(used => used.Count >= atLeast)
            .OrderByDescending(used => used.Count)
            .Select(used => used.Image)
            .ToList();
    }
}
