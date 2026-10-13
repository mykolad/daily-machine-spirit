using DailyMachineSpirit.Functions.Voters;

namespace DailyMachineSpirit.Functions.Caching;

/// <summary>The Cloudflare zone in front of the site, whose cached pages the app purges when they change.</summary>
public class CloudflareOptions
{
    public const string SectionName = "Cloudflare";

    /// <summary>The site's address as readers reach it, e.g. <c>https://dailymachinespirit.fyi</c>: purges name full URLs.</summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>The zone's id (Cloudflare dashboard → the domain → Overview); not secret.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>A token that may only purge this zone's cache, a Key Vault reference in Azure (the vault's <c>CloudflareApiToken</c>).</summary>
    public string ApiToken { get; set; } = string.Empty;

    public bool IsConfigured => SettingValue.IsSet(SiteUrl) && SettingValue.IsSet(ZoneId) && SettingValue.IsSet(ApiToken);
}
