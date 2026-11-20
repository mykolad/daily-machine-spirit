using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Which rites the Reliquary keeps in its "Most confessed" order (#200): those the signed-in readers confessed to having
/// done, the most first; a tie goes to the newer. As many as the most venerated (<see cref="Veneration.Kept"/>).
/// </summary>
public static class Confession
{
    /// <summary>The numbers of the most confessed rites, the most first.</summary>
    public static List<int> MostConfessed(IReadOnlyList<RiteVotes> votes)
        => votes
            .Where(rite => rite.Confessions > 0)
            .OrderByDescending(rite => rite.Confessions)
            .ThenByDescending(rite => rite.Number)
            .Take(Veneration.Kept)
            .Select(rite => rite.Number)
            .ToList();

    /// <summary>Each kept rite's place among the most confessed, by its number: 1 for the most.</summary>
    public static IReadOnlyDictionary<int, int> Places(IReadOnlyList<RiteVotes> votes)
        => MostConfessed(votes).Select((number, at) => (number, at)).ToDictionary(kept => kept.number, kept => kept.at + 1);
}
