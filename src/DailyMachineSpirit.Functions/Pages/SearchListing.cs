using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// What a page tells search engines and link previews about itself, beyond its title and description: its one
/// canonical address (a page reached with a tracking parameter is still the same page), or that it belongs in no index.
/// </summary>
public sealed record SearchListing
{
    /// <summary>
    /// For a page that's no page of its own in an index: a search's results (whatever anyone typed must never become a
    /// page in an index), a reader's own page, an error.
    /// </summary>
    public static readonly SearchListing Unlisted = new(None);

    private SearchListing(Option<string> canonical)
    {
        Canonical = canonical;
    }

    /// <summary>The page's address in full, from the site's origin (<see cref="SiteOrigin"/>); None when unlisted.</summary>
    public Option<string> Canonical { get; }

    /// <summary>Open Graph's kind of page: <c>website</c>, or <c>article</c> for a rite's own page.</summary>
    public string OpenGraphType { get; init; } = "website";

    /// <summary>JSON-LD describing the page's subject (<see cref="RiteStructuredData"/>), when it has one.</summary>
    public Option<string> StructuredData { get; init; }

    public static SearchListing At(string origin, string path) => new(Some(origin + path));
}
