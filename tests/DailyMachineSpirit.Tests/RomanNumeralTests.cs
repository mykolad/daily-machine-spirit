using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class RomanNumeralTests
{
    [Theory]
    [InlineData(2026, "MMXXVI")]
    [InlineData(1999, "MCMXCIX")]
    [InlineData(2049, "MMXLIX")]
    [InlineData(4, "IV")]
    public void Of_WritesTheYearAsTheMonthRubricsDo(int year, string expected)
        => Assert.Equal(expected, RomanNumeral.Of(year));
}
