namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The pages for when there's no rite to show: a failure, or a link to a rite or a subject that doesn't exist.</summary>
public static class StatePages
{
    /// <summary>The data couldn't be read (served as 503). "Try again" reloads the same address.</summary>
    public static string Failed(string path)
        => Layout.Page("The rite has failed", "The Machine Spirit did not answer.", Section.Other, $"""
            <div class="state" role="alert">
            <h1>The rite has failed.</h1>
            <p class="state-body">The Machine Spirit did not answer. This is usually the network, not a curse.</p>
            <code class="state-code">GET {Html.Encode(path)} → 503 Service Unavailable</code>
            <div class="state-actions"><a class="button button-primary" href="{Html.Encode(path)}">{Svg.RefreshIcon}Try again</a></div>
            </div>
            """);

    /// <param name="id">What the address named, shown back as it was typed.</param>
    public static string NotFound(string id)
        => Layout.Page("No such rite exists", "No such rite exists.", Section.Other, $"""
            <div class="state">
            {Svg.AwakeSkull}
            <p class="state-eyebrow">ERROR 404</p>
            <h1>No such rite exists.</h1>
            <p class="state-body">There is no litany with the id “{Html.Encode(id)}”. It was never written, or the link was copied wrong.</p>
            <div class="state-actions">
            <a class="button button-primary" href="{Paths.Today}">Read today’s litany</a>
            <a class="button button-outline" href="{Paths.Archive}">Browse the Archive</a>
            </div>
            </div>
            """);

    /// <param name="key">The tag the address named, shown back as it was typed.</param>
    public static string NoSuchTag(string key)
        => Layout.Page("No such subject", "No such subject.", Section.Other, $"""
            <div class="state">
            {Svg.AwakeSkull}
            <p class="state-eyebrow">ERROR 404</p>
            <h1>No such subject.</h1>
            <p class="state-body">No rite is filed under “{Html.Encode(key)}”. The link was copied wrong, or the subject was never one of ours.</p>
            <div class="state-actions">
            <a class="button button-primary" href="{Paths.Today}">Read today’s litany</a>
            <a class="button button-outline" href="{Paths.Archive}">Browse the Archive</a>
            </div>
            </div>
            """);
}
