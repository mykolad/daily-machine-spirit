namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The name the record (#9) shows for one Scribe, by their Cloudflare Access id. A list entry
/// (<c>Scriptorium:Scribes:0:Id</c>, <c>:Name</c>) rather than a key per id: an app setting's name can't hold the id's
/// hyphens.
/// </summary>
public sealed class ScribeName
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
