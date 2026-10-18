namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>Jev, the judge (<c>machine-spirit.json</c>, <c>Jev</c>); its key comes from the app's settings.</summary>
public sealed record JevOptions
{
    public const string SectionName = "Jev";

    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Pinned, so scores made months apart stay comparable. A new version means judging every rite again.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>A Key Vault reference in Azure (the vault's <c>JevApiKey</c>); empty turns judging off.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public Patience Patience { get; init; } = new();

    /// <summary>A topic becomes a tag at this probability or more.</summary>
    public float TagMinProbability { get; init; }

    public int TagsPerRite { get; init; }

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
