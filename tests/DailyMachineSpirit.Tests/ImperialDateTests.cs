using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

/// <summary>A rite's day as the far future reckons it.</summary>
public sealed class ImperialDateTests
{
    [Theory]
    [InlineData(2026, 1, 1, "0 000 026.M3")]
    [InlineData(2026, 10, 9, "0 769 026.M3")]
    [InlineData(2026, 10, 22, "0 805 026.M3")]
    [InlineData(2026, 12, 31, "0 997 026.M3")]
    [InlineData(2028, 12, 31, "0 997 028.M3")]
    [InlineData(3000, 7, 1, "0 495 000.M4")]
    public void Of_CountsTheThousandthsOfTheYearPassed_TheYearAndTheMillennium(int year, int month, int day, string expected)
        => Assert.Equal(expected, ImperialDate.Of(new DateOnly(year, month, day)));

    [Fact]
    public void WithTheDay_DescribesTheButtonWithTheDayAsWeReckonIt()
    {
        var html = ImperialDate.WithTheDay(new DateOnly(2026, 10, 22), "rite-day", "rite-date");

        Assert.Equal(
            """<span class="imperial rite-date"><button type="button" class="imperial-date" aria-describedby="rite-day"><time datetime="2026-10-22">0 805 026.M3</time></button><span class="imperial-day" id="rite-day" role="tooltip">Thursday 22 October 2026</span></span>""",
            html);
    }

    [Fact]
    public void InALink_SaysTheDayToAScreenReader_AndWhereThePointerRests()
        => Assert.Equal(
            """<time class="row-date" datetime="2026-10-22" title="Thursday 22 October 2026"><span aria-hidden="true">0 805 026.M3</span><span class="sr-only">Thursday 22 October 2026</span></time>""",
            ImperialDate.InALink(new DateOnly(2026, 10, 22), "row-date"));
}
