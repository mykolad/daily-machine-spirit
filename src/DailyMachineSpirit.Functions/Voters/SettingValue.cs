namespace DailyMachineSpirit.Functions.Voters;

public static class SettingValue
{
    // A Key Vault reference whose secret isn't set yet reaches the app as its own text: that's not a value either.
    public static bool IsSet(string value)
        => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("@Microsoft.KeyVault(", StringComparison.Ordinal);
}
