namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Where the rites the Reliquary keeps stand in each of its orders, by number: among the most venerated
/// (<see cref="Veneration.Places"/>) and the most confessed (<see cref="Confession.Places"/>). Rows wear a badge for each.
/// </summary>
public sealed record RelicStandings(IReadOnlyDictionary<int, int> Venerated, IReadOnlyDictionary<int, int> Confessed)
{
    /// <summary>For pages that show no rite's place: the Reliquary's own, whose order says it, or when the counts can't be read.</summary>
    public static RelicStandings None { get; } = new(Veneration.NoPlaces, Veneration.NoPlaces);
}
