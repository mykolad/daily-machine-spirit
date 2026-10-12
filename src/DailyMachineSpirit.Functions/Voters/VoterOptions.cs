namespace DailyMachineSpirit.Functions.Voters;

/// <summary>The two secrets behind signed-in voting, Key Vault references in Azure.</summary>
public class VoterOptions
{
    public const string SectionName = "Voters";

    // A signed-in reader sees both what goes into these HMACs and what comes out (their voter key, their cookie), so a
    // short secret could be guessed offline. Shorter ones count as not set: nobody signs in. The README's commands make
    // them from 32 random bytes (44 characters).
    public const int MinSecretLength = 32;

    /// <summary>
    /// Keys each account's votes. Changing it makes every account a new voter that can vote again, so it's set once
    /// (the vault's <c>VoterKeySecret</c>).
    /// </summary>
    public string KeySecret { get; set; } = string.Empty;

    /// <summary>Signs the sign-in cookie. Changing it only signs everyone out (the vault's <c>VoterSessionSecret</c>).</summary>
    public string SessionSecret { get; set; } = string.Empty;

    public bool IsConfigured => IsStrong(KeySecret) && IsStrong(SessionSecret);

    private static bool IsStrong(string secret) => SettingValue.IsSet(secret) && secret.Trim().Length >= MinSecretLength;
}
