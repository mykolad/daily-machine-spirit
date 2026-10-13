namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The site's URLs, in one place so links and routes can't drift apart.</summary>
public static class Paths
{
    public const string Today = "/";

    public const string Archive = "/archive";

    public const string Search = "/search";

    public static string Rite(int number) => $"/r/{number}";

    public static string ArchiveBefore(int number) => $"{Archive}?before={number}";

    public static string Tag(string key) => $"/tags/{key}";

    public static string TagBefore(string key, int number) => $"{Tag(key)}?before={number}";

    public static string SearchFor(string typed) => $"{Search}?q={Uri.EscapeDataString(typed)}";

    public static string SearchBefore(string typed, int number) => $"{SearchFor(typed)}&before={number}";

    public static string SignIn(string returnTo) => $"/auth/github?return={Uri.EscapeDataString(returnTo)}";

    public static string SignOutFrom(string returnTo) => $"/auth/signout?return={Uri.EscapeDataString(returnTo)}";
}
