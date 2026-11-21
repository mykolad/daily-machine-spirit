using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The offices a signed-in reader rises through by the rites they judge (#201): voted on or confessed to, each rite once.
/// Worked out from their votes whenever it's shown, so nothing is stored; only they see it, in their Breviary and in the
/// toast when a vote raises them. The site's own words, not the setting's ranks. Thresholds aren't worth a setting.
/// </summary>
public static class Ranks
{
    public static readonly IReadOnlyList<Rank> All =
    [
        new("Supplicant", "a", 0, Svg.SupplicantInsignia),
        new("Initiate", "an", 1, Svg.InitiateInsignia),
        new("Acolyte", "an", 10, Svg.AcolyteInsignia),
        new("Lector", "a", 25, Svg.LectorInsignia),
        new("Keeper of Rites", "a", 50, Svg.KeeperOfRitesInsignia),
        new("Archivist", "an", 100, Svg.ArchivistInsignia),
        new("Elder of the Shrine", "an", 250, Svg.ElderOfTheShrineInsignia),
    ];

    /// <summary>The highest office the count of rites judged has reached.</summary>
    public static Rank Of(int judged) => All.Last(rank => judged >= rank.Threshold);

    /// <summary>
    /// The office the last of <paramref name="judged"/> rites raised the reader to, if it did. None for a count that
    /// crossed no threshold, or for none at all (another tab took the vote back before it was counted).
    /// </summary>
    public static Option<Rank> RaisedBy(int judged) => judged > 0 && Of(judged) != Of(judged - 1) ? Some(Of(judged)) : None;

    /// <summary>The office after it, or None at the highest.</summary>
    public static Option<Rank> After(Rank rank) => All.SkipWhile(office => office != rank).Skip(1).HeadOrNone();

    /// <summary>"9 more rites to Lector", or what the highest office says instead.</summary>
    public static string ToNext(int judged)
        => After(Of(judged)).Match(
            Some: next => $"{next.Threshold - judged} more {(next.Threshold - judged == 1 ? "rite" : "rites")} to {next.Name}",
            None: () => "The shrine has no higher office");

    /// <summary>How far from this office to the next, in whole percent; 100 at the highest.</summary>
    public static int Progress(int judged)
    {
        var rank = Of(judged);
        return After(rank).Match(
            Some: next => (int)Math.Round(100.0 * (judged - rank.Threshold) / (next.Threshold - rank.Threshold)),
            None: () => 100);
    }
}
