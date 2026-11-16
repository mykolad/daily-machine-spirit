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

    /// <summary>The watched images used at least <paramref name="atLeast"/> times in the texts, the most used first.</summary>
    public static List<string> In(IEnumerable<string> texts, int atLeast)
    {
        var all = string.Join('\n', texts);
        return Watched
            .Select(image => (Image: image, Count: Regex.Count(all, $@"\b{Regex.Escape(image)}s?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            .Where(used => used.Count >= atLeast)
            .OrderByDescending(used => used.Count)
            .Select(used => used.Image)
            .ToList();
    }
}
