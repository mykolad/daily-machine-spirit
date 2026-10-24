namespace DailyMachineSpirit.Mcp;

public sealed class RitesApiOptions
{
    public const string SectionName = "RitesApi";

    /// <summary>
    /// Where the public rites API is read (docs/api.md): the site's address through Cloudflare, so the tools are answered
    /// from Cloudflare's copies, as any other client is.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
