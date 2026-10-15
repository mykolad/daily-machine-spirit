using System.Text;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A year as the Archive's month rubrics write it: 2026 is MMXXVI.</summary>
public static class RomanNumeral
{
    private static readonly (int Value, string Letters)[] Steps =
        [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];

    /// <param name="number">From 1 to 3999.</param>
    public static string Of(int number)
    {
        var letters = new StringBuilder();
        var left = number;
        foreach (var (value, step) in Steps)
        {
            for (; left >= value; left -= value)
            {
                letters.Append(step);
            }
        }
        return letters.ToString();
    }
}
