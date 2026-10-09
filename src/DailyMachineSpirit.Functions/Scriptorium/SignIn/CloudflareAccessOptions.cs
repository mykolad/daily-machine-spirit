namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>The Cloudflare Access application that guards the Scriptorium (Zero Trust → Access → Applications).</summary>
public class CloudflareAccessOptions
{
    public const string SectionName = "CloudflareAccess";

    /// <summary>The Zero Trust team's domain, e.g. <c>example.cloudflareaccess.com</c>: it issues and signs the tokens.</summary>
    public string TeamDomain { get; set; } = string.Empty;

    /// <summary>The Access application's Audience (AUD) tag: tokens for other applications aren't accepted.</summary>
    public string Audience { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TeamDomain) && !string.IsNullOrWhiteSpace(Audience);
}
