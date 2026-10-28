using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Which rites the Reliquary keeps: those blessed more than called heresy, the most venerated first. A rite's
/// veneration is its blessings less its heresies, so one vote each way cancels out; a tie goes to the more blessed,
/// then the newer.
/// </summary>
public static class Veneration
{
    /// <summary>How many rites the Reliquary keeps: a page's worth.</summary>
    public const int Kept = 20;

    /// <summary>For pages that show no rite's place: the Reliquary's own, whose order says it, or when the counts can't be read.</summary>
    public static readonly IReadOnlyDictionary<int, int> NoPlaces = new Dictionary<int, int>();

    /// <summary>The numbers of the most venerated rites, the most first.</summary>
    public static List<int> MostVenerated(IReadOnlyList<RiteVotes> votes)
        => votes
            .Where(rite => rite.Blessed - rite.Heresy > 0)
            .OrderByDescending(rite => rite.Blessed - rite.Heresy)
            .ThenByDescending(rite => rite.Blessed)
            .ThenByDescending(rite => rite.Number)
            .Take(Kept)
            .Select(rite => rite.Number)
            .ToList();

    /// <summary>Each kept rite's place in the Reliquary, by its number: 1 for the most venerated.</summary>
    public static IReadOnlyDictionary<int, int> Places(IReadOnlyList<RiteVotes> votes)
        => MostVenerated(votes).Select((number, at) => (number, at)).ToDictionary(kept => kept.number, kept => kept.at + 1);
}
