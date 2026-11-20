namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// What a rite kept in the Reliquary wears on its card and its rows: its place there, linking to it, so readers find the
/// Reliquary through the rites in it.
/// </summary>
public static class RelicBadge
{
    /// <param name="places">Each kept rite's place, by its number (<see cref="Veneration.Places"/>).</param>
    /// <returns>The badge, or empty for a rite the Reliquary doesn't keep.</returns>
    public static string For(int number, IReadOnlyDictionary<int, int> places)
        => places.TryGetValue(number, out var place)
            ? $"""<a class="relic" href="{Paths.Reliquary}">{Svg.SmallFlameIcon}{Words(place)}<span class="sr-only">, in the Reliquary</span></a>"""
            : string.Empty;

    private static string Words(int place) => place == 1 ? "Most venerated" : $"{Ordinals.Of(place)} most venerated";
}
