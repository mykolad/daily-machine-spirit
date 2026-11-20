namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A place in words: 1st, 2nd, 3rd, 4th… 11th, 12th, 13th… 21st, as the relic badges say it.</summary>
public static class Ordinals
{
    public static string Of(int place) => (place % 100) switch
    {
        11 or 12 or 13 => $"{place}th",
        _ => (place % 10) switch
        {
            1 => $"{place}st",
            2 => $"{place}nd",
            3 => $"{place}rd",
            _ => $"{place}th",
        },
    };
}
