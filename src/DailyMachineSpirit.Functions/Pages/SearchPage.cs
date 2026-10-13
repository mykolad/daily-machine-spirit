using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Search (<c>/search?q=</c>): a plain form that works without the script, then the rites holding every word, newest
/// first, a page at a time like the archive. How many were found is in the page's title too, which a screen reader
/// reads first when the results load.
/// </summary>
public static class SearchPage
{
    private const string Description = "Find rites by the words in them.";

    /// <param name="typed">What the reader typed, shown back in the field.</param>
    /// <param name="results">None before anything is searched for.</param>
    public static string Render(string typed, Option<SearchResults> results)
    {
        var quoted = $"“{typed}”";
        var title = results.Match(
            Some: found => $"{quoted}: {Rites(found.Found)}",
            None: () => "Search");
        var shown = results.Match(
            Some: found => $"""
                <p class="search-count">{Html.Encode(Count(found.Found, quoted))}</p>
                {(found.Found == 0 ? string.Empty : RiteRows.Render(found.Shown, found.OlderThan.Map(number => Paths.SearchBefore(typed, number)), "No earlier rite holds these words."))}
                """,
            None: () => string.Empty);
        return Layout.Page(title, Description, Section.Search, $"""
            <h1 class="archive-title">Search the Archive</h1>
            <p class="archive-subtitle">{Description}</p>
            <div class="archive-divider">{Svg.BrassDivider}</div>
            <form class="search" role="search" action="{Paths.Search}" method="get">
            <label for="search-words">Words in the rite</label>
            <div class="search-row">
            <input type="search" id="search-words" name="q" value="{Html.Encode(typed)}" maxlength="{SearchWords.MaxLength}" autocomplete="off" spellcheck="false">
            <button type="submit" class="button button-primary">Search</button>
            </div>
            </form>
            {shown}
            """);
    }

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";

    private static string Count(int count, string quoted) => count switch
    {
        0 => $"No rite holds {quoted}.",
        1 => $"1 rite holds {quoted}.",
        _ => $"{Html.Count(count)} rites hold {quoted}.",
    };
}
