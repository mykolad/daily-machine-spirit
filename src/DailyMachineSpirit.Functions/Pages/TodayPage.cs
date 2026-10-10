using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The home page: the masthead, the newest rite and the countdown to the next.</summary>
public static class TodayPage
{
    private const string Tagline = "In the grim darkness of the far future, no one reads the code.";

    private const string Masthead = $"""
        <div class="masthead">
        {Svg.MastheadRule}
        <h1>The Daily Machine Spirit</h1>
        <p class="tagline">{Tagline}</p>
        </div>
        """;

    public static string Render(Rite rite, DateTimeOffset now)
        => Layout.Page(string.Empty, Html.FirstLine(rite.Text.Replace("`", string.Empty)), Section.Today, $"""
            {Masthead}
            {RiteCard.Render(rite, ownPage: false)}
            <div class="countdown">
            <div class="countdown-line">{Svg.CandleLeft}<p>Next litany in <span class="amber" data-countdown>{Countdown.ToNextRite(now)}</span></p>{Svg.CandleRight}</div>
            <p class="countdown-note">A new rite is published every day at 00:00 UTC. <a href="{Paths.Archive}">Browse the Archive</a></p>
            </div>
            """);

    /// <summary>Before the first rite: the Machine Spirit sleeps until midnight.</summary>
    public static string Slumbering(DateTimeOffset now)
        => Layout.Page(string.Empty, Tagline, Section.Today, $"""
            {Masthead}
            <div class="state">
            {Svg.SleepingSkull}
            <h2>The Machine Spirit slumbers.</h2>
            <p class="state-body">The first litany arrives in <strong class="amber" data-countdown>{Countdown.ToNextRite(now)}</strong>.</p>
            </div>
            """);
}
