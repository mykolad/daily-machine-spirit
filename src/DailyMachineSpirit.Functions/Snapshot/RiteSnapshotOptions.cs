namespace DailyMachineSpirit.Functions.Snapshot;

/// <summary>Where the rites' snapshot is kept (#112): an app setting from <c>infra/</c>, which differs per environment.</summary>
public sealed record RiteSnapshotOptions
{
    public const string SectionName = "RiteSnapshot";

    /// <summary>The blob container's address, outside the app's region; empty (a local run) keeps no snapshot.</summary>
    public string ContainerUri { get; init; } = string.Empty;
}
