using System.Reflection;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Mcp;

/// <summary>
/// The commit this build came from, which <c>/healthz</c> reports (the deploys' smoke tests wait for it), read as the
/// site reads its own (<c>AppVersion</c>): the .NET SDK appends it to the informational version inside a git checkout.
/// </summary>
public static class ConnectorVersion
{
    public const string Local = "dev";

    public static string Short { get; } = FromInformationalVersion(
        Optional(typeof(ConnectorVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>())
            .Map(attribute => attribute.InformationalVersion));

    /// <summary>The first 7 characters of the commit, or <see cref="Local"/> when the build has none.</summary>
    internal static string FromInformationalVersion(Option<string> informationalVersion)
        => informationalVersion
            .Bind(version => version.IndexOf('+') is var plus and >= 0 ? Some(version[(plus + 1)..]) : None)
            .Filter(sha => sha.Length > 0)
            .Map(sha => sha[..Math.Min(7, sha.Length)])
            .IfNone(Local);
}
