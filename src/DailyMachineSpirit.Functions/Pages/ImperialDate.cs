using System.Globalization;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A rite's day as the far future reckons it: a check number, the part of the year passed (in thousandths, at the
/// rite's midnight, 00:00 UTC), the year within its millennium, and the millennium: 22 October 2026 is
/// "0 805 026.M3". The pages show it in place of the day, with the day as we reckon it a tap, a focus or a pointer away
/// (<see cref="WithTheDay"/>), and said to a screen reader.
/// </summary>
public static class ImperialDate
{
    // The check number says how the date was known; 0, Terra's own reckoning, is the surest.
    private const int CheckNumber = 0;

    /// <summary>"0 805 026.M3".</summary>
    public static string Of(DateOnly day)
    {
        var daysInYear = DateTime.IsLeapYear(day.Year) ? 366 : 365;
        var thousandths = (day.DayOfYear - 1) * 1000 / daysInYear;
        return string.Create(CultureInfo.InvariantCulture, $"{CheckNumber} {thousandths:000} {day.Year % 1000:000}.M{day.Year / 1000 + 1}");
    }

    /// <summary>"Thursday 22 October 2026": the day as we reckon it, which the Imperial date stands for.</summary>
    public static string Day(DateOnly day) => day.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// The Imperial date as a toggletip: a button showing the day as we reckon it while the pointer is over it, while it
    /// has focus, or once tapped (the page's script), and describing the button with it, so a screen reader says both.
    /// <paramref name="id"/> names the day's text, once per page.
    /// </summary>
    public static string WithTheDay(DateOnly day, string id, string className)
        => $"""<span class="imperial {className}"><button type="button" class="imperial-date" aria-describedby="{id}"><time datetime="{Html.IsoDate(day)}">{Of(day)}</time></button><span class="imperial-day" id="{id}" role="tooltip">{Day(day)}</span></span>""";

    /// <summary>
    /// The Imperial date inside a link, where a button can't be: the day as we reckon it is said to a screen reader and
    /// shown where the pointer rests (its <c>title</c>), and the rite's own page has it as a toggletip.
    /// </summary>
    public static string InALink(DateOnly day, string className)
        => $"""<time class="{className}" datetime="{Html.IsoDate(day)}" title="{Day(day)}"><span aria-hidden="true">{Of(day)}</span><span class="sr-only">{Day(day)}</span></time>""";
}
