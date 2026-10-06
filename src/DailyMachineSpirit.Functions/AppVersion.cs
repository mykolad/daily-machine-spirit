using System.Reflection;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions;

/// <summary>
/// The commit this build came from. The .NET SDK appends it to the assembly's informational version
/// ("1.0.0+&lt;sha&gt;") when it builds inside a git checkout, so deploys need no extra setting.
/// </summary>
public static class AppVersion
{
    public const string Local = "dev";

    public static string Short { get; } = FromInformationalVersion(
        Optional(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>())
            .Map(attribute => attribute.InformationalVersion));

    /// <summary>The first 7 characters of the commit, or <see cref="Local"/> when the build has none.</summary>
    internal static string FromInformationalVersion(Option<string> informationalVersion)
        => informationalVersion
            .Bind(version => version.IndexOf('+') is var plus and >= 0 ? Some(version[(plus + 1)..]) : None)
            .Filter(sha => sha.Length > 0)
            .Map(sha => sha[..Math.Min(7, sha.Length)])
            .IfNone(Local);
}
