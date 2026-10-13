namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The site's URLs, in one place so links and routes can't drift apart.</summary>
public static class Paths
{
    public const string Today = "/";

    public const string Archive = "/archive";

    public static string Rite(int number) => $"/r/{number}";

    public static string ArchiveBefore(int number) => $"{Archive}?before={number}";

    public static string Tag(string key) => $"/tags/{key}";

    public static string TagBefore(string key, int number) => $"{Tag(key)}?before={number}";

    /// <summary>
    /// The cached pages that show a rite's counts and change with its votes: its own, and Today, which shows the newest
    /// (purging it for an older rite costs one render). The archive and a tag's page show counts too, but which of their
    /// pages holds a rite moves as rites are added, so they're only cached briefly (<see cref="SitePages"/>).
    /// </summary>
    public static IReadOnlyCollection<string> ShowingVotesOf(int number) => [Today, Rite(number)];

    public static string SignIn(string returnTo) => $"/auth/github?return={Uri.EscapeDataString(returnTo)}";

    public static string SignOutFrom(string returnTo) => $"/auth/signout?return={Uri.EscapeDataString(returnTo)}";
}
