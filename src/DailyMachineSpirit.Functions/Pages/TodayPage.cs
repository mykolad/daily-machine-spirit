using System.Text;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The home page: the masthead, the newest rite, the countdown to the next, and the rites before it.</summary>
public static class TodayPage
{
    // The motto under the day's rite, between brass rules, where it's read; on Today it leaves the footer, so it's said once.
    private const string Epigraph = $"""<p class="epigraph">{Layout.Motto}</p>""";

    /// <param name="recent">The rites before today's, newest first; the section is left out when there are none.</param>
    public static string Render(Rite rite, IReadOnlyList<Rite> recent, DateTimeOffset now)
    {
        var countdown = Countdown.ToNextRite(now);
        // On a wide screen the countdown moves to the left margin, and the line under the actions (with its candles)
        // hides, so one countdown shows at a time.
        var marginCountdown = $"""<div class="margin-countdown"><p class="margin-label">Next litany in</p><p class="margin-value" {Countdown.Attribute(now)}>{countdown}</p></div>""";
        return Layout.Page(string.Empty, Html.FirstLine(rite.Writing.Text.Replace("`", string.Empty)), Section.Today, $"""
            {Masthead.Render(isHeading: true)}
            {RiteCard.Render(rite, ownPage: false, countedAt: now, marginNotes: marginCountdown)}
            <div class="countdown">
            <div class="countdown-line">{Svg.CandleLeft}<p>Next litany in <span class="amber" {Countdown.Attribute(now)}>{countdown}</span></p>{Svg.CandleRight}</div>
            <p class="countdown-note">A new rite is published every day at 00:00 UTC.</p>
            </div>
            {Epigraph}
            {Recent(recent)}
            """);
    }

    /// <summary>Before the first rite: the Machine Spirit sleeps until midnight.</summary>
    public static string Slumbering(DateTimeOffset now)
        => Layout.Page(string.Empty, Masthead.Tagline, Section.Today, $"""
            {Masthead.Render(isHeading: true)}
            <div class="state">
            {Svg.SleepingSkull}
            <h2>The Machine Spirit slumbers.</h2>
            <p class="state-body">The first litany arrives in <strong class="amber" {Countdown.Attribute(now)}>{Countdown.ToNextRite(now)}</strong>.</p>
            </div>
            {Epigraph}
            """);

    // Small parchments, kind and day and title alone: enough to pick one, and the archive is a link away. Each card is
    // one link, its focus ring outside it on the dark ground.
    private static string Recent(IReadOnlyList<Rite> recent)
    {
        if (recent.Count == 0)
        {
            return string.Empty;
        }

        var cards = new StringBuilder();
        foreach (var other in recent)
        {
            cards.Append($"""
                <li><a class="recent-card" href="{Paths.Rite(other.Number)}">
                <span class="recent-kicker">A {(other.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")} · {ImperialDate.InALink(other.PublishedOnUtc, "recent-date")}</span>
                <span class="recent-title">{Html.Encode(other.Writing.Title)}</span>
                {Svg.SmallSeal}
                </a></li>
                """);
        }

        return $"""
            <section class="recent" aria-labelledby="recent-rites">
            <h2 id="recent-rites">Recent rites</h2>
            <ul class="recent-list">{cards}</ul>
            <a class="recent-archive" href="{Paths.Archive}">Open the Archive<span aria-hidden="true"> →</span></a>
            </section>
            """;
    }
}
