namespace DailyMachineSpirit.Functions.Pages;

/// <summary>How long Cloudflare may keep a page (<see cref="SitePages"/>); browsers keep every page a minute.</summary>
public enum EdgeLifetime
{
    /// <summary>A minute, as browsers do: no purge reaches the page when it changes.</summary>
    Brief,

    /// <summary>An hour, unless the app purges it sooner (<see cref="Paths.ShowingVotesOf"/>).</summary>
    UntilPurged,
}
