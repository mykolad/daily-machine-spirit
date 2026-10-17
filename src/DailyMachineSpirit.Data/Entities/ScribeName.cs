namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// The name a Scribe chose for the Record (#84), by their Cloudflare Access id: a name or an alias, never an email. Only
/// the Scribes see it.
/// </summary>
public sealed record ScribeName(string AccessId, string Name)
{
    public const int MaxLength = 40;
}
