namespace DailyMachineSpirit.Functions.Voters;

/// <summary>The two secrets behind signed-in voting, Key Vault references in Azure.</summary>
public class VoterOptions
{
    public const string SectionName = "Voters";

    /// <summary>
    /// Keys each account's votes. Changing it makes every account a new voter that can vote again, so it's set once
    /// (the vault's <c>VoterKeySecret</c>).
    /// </summary>
    public string KeySecret { get; set; } = string.Empty;

    /// <summary>Signs the sign-in cookie. Changing it only signs everyone out (the vault's <c>VoterSessionSecret</c>).</summary>
    public string SessionSecret { get; set; } = string.Empty;

    public bool IsConfigured => SettingValue.IsSet(KeySecret) && SettingValue.IsSet(SessionSecret);
}
