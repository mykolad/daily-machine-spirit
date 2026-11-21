namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// What a rite among the most confessed (#200) wears on its rows, beside the venerated badge: its place in the
/// Reliquary's confessed order, linking to it.
/// </summary>
public static class ConfessionBadge
{
    /// <param name="places">Each kept rite's place, by its number (<see cref="Confession.Places"/>).</param>
    /// <returns>The badge, or empty for a rite the order doesn't keep.</returns>
    public static string For(int number, IReadOnlyDictionary<int, int> places)
        => places.TryGetValue(number, out var place)
            ? $"""<a class="relic" href="{Paths.ReliquaryConfessed}">{Svg.SmallCandleIcon}{Words(place)}<span class="sr-only">, among the most confessed</span></a>"""
            : string.Empty;

    private static string Words(int place) => place == 1 ? "Most confessed" : $"{Ordinals.Of(place)} most confessed";
}
