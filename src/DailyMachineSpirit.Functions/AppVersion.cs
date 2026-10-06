using System.Reflection;

namespace DailyMachineSpirit.Functions;

/// <summary>
/// The commit this build came from. The .NET SDK appends it to the assembly's informational version
/// ("1.0.0+&lt;sha&gt;") when it builds inside a git checkout, so deploys need no extra setting.
/// </summary>
public static class AppVersion
{
    public const string Local = "dev";

    public static string Short { get; } = FromInformationalVersion(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>The first 7 characters of the commit, or <see cref="Local"/> when the build has none.</summary>
    internal static string FromInformationalVersion(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        if (plus < 0)
            return Local;
        var sha = informationalVersion![(plus + 1)..];
        return sha.Length == 0 ? Local : sha[..Math.Min(7, sha.Length)];
    }
}
