namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A Scribe, by their Cloudflare Access id (the token's <c>sub</c>), never a name or an email. Stored as the plain id,
/// so the documents keep it as they always have.
/// </summary>
public sealed record ScribeId(string Value)
{
    /// <summary>Staging's one shared Scribe: its Scriptorium has no sign-in, and its IP rule admits its owner alone.</summary>
    public static ScribeId Staging { get; } = new("staging");

    public override string ToString() => Value;
}
