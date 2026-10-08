namespace DailyMachineSpirit.Functions.Generation.Scoring;

public class JevOptions
{
    public const string SectionName = "Jev";

    public string Endpoint { get; set; } = "https://jevtypesafeai.com/api/v1/decide";

    /// <summary>Pinned, so scores made months apart stay comparable. A new version means scoring every rite again.</summary>
    public string Model { get; set; } = "jev-1.13.0";

    /// <summary>A Key Vault reference in Azure (the vault's <c>JevApiKey</c>); empty turns scoring off.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
