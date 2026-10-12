namespace DailyMachineSpirit.Functions.Voters;

/// <summary>The GitHub OAuth app readers sign in with (GitHub → Settings → Developer settings → OAuth Apps).</summary>
public class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string ClientId { get; set; } = string.Empty;

    /// <summary>A Key Vault reference in Azure (the vault's <c>GitHubClientSecret</c>).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The OAuth app's callback URL, exactly: GitHub sends readers back only there.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    public bool IsConfigured => SettingValue.IsSet(ClientId) && SettingValue.IsSet(ClientSecret) && SettingValue.IsSet(CallbackUrl);
}
