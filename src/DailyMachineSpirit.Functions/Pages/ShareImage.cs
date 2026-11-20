namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The picture a link preview shows for a page (#191), 1200 × 630: the site's card, or a rite's own. Its alt text is
/// what a screen reader says for it, so for a rite it's the joke itself.
/// </summary>
public sealed record ShareImage(string Path, string Alt)
{
    public const int Width = 1200;
    public const int Height = 630;

    public static readonly ShareImage Site = new(
        Paths.SiteCard, $"{Layout.SiteName}: In the grim darkness of the far future, no one reads the code. A new rite every day at 00:00 UTC.");
}
