namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The site's URLs, in one place so links and routes can't drift apart.</summary>
public static class Paths
{
    public const string Today = "/";

    public const string Archive = "/archive";

    public static string Rite(int number) => $"/r/{number}";

    public static string ArchiveBefore(int number) => $"{Archive}?before={number}";
}
