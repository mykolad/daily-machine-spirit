using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The site's URLs, in one place so links and routes can't drift apart.</summary>
public static class Paths
{
    public const string Today = "/";

    public const string Archive = "/archive";

    public const string About = "/about";

    /// <summary>
    /// Not the rites, but what the nav's search field matches them on as the reader types: each rite's number, day,
    /// kind and words (<c>SearchIndexEntry</c>).
    /// </summary>
    public const string SearchIndex = "/api/search-index";

    /// <summary>The public rites API, version 1: every published rite, for clients outside the site (<c>docs/api.md</c>).</summary>
    public const string RitesApi = "/api/v1/rites";

    /// <summary>One rite's Heretical Truth in the rites API, sealed in the list until a client asks for it.</summary>
    public static string HereticalTruthApi(int number) => $"{RitesApi}/{number}/heretical-truth";

    public static string Rite(int number) => $"/r/{number}";

    public static string ArchiveBefore(int number) => $"{Archive}?before={number}";

    public static string Tag(string key) => $"/tags/{key}";

    public static string TagBefore(string key, int number) => $"{Tag(key)}?before={number}";

    /// <summary>The Archive's search results for what the reader typed.</summary>
    public static string ArchiveSearch(string typed) => ArchiveSearch(typed, None);

    /// <summary>The Archive's search results for what the reader typed, among the rites filed under the tag if one is given.</summary>
    public static string ArchiveSearch(string typed, Option<string> tagKey)
        => $"{Archive}?q={Uri.EscapeDataString(typed)}{tagKey.Map(key => $"&tag={Uri.EscapeDataString(key)}").IfNone(string.Empty)}";

    public static string ArchiveSearchBefore(string typed, int number) => ArchiveSearchBefore(typed, None, number);

    public static string ArchiveSearchBefore(string typed, Option<string> tagKey, int number) => $"{ArchiveSearch(typed, tagKey)}&before={number}";

    /// <summary>
    /// The cached pages that show a rite's counts and change with its votes: its own, and Today, which shows the newest
    /// (purging it for an older rite costs one render). The archive, a tag's page and search results show counts too,
    /// but which of their pages holds a rite moves as rites are added, so they're only cached briefly
    /// (<see cref="SitePages"/>).
    /// </summary>
    public static IReadOnlyCollection<string> ShowingVotesOf(int number) => [Today, Rite(number)];

    public static string SignIn(string returnTo) => $"/auth/github?return={Uri.EscapeDataString(returnTo)}";

    public static string SignOutFrom(string returnTo) => $"/auth/signout?return={Uri.EscapeDataString(returnTo)}";
}
