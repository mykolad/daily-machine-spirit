using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// Who did what the Record (#9) records: a Scribe, or the Machine Spirit acting on its own (writing, publishing,
/// distilling). Stored as the Scribe's id, or as <c>machine-spirit</c>.
/// </summary>
public sealed record Actor
{
    private const string MachineSpiritId = "machine-spirit";

    private Actor(string id)
    {
        Id = id;
    }

    public static Actor MachineSpirit { get; } = new(MachineSpiritId);

    /// <summary>As stored.</summary>
    public string Id { get; }

    public bool IsMachineSpirit => Id == MachineSpiritId;

    public Option<ScribeId> Scribe => IsMachineSpirit ? None : Some(new ScribeId(Id));

    public static Actor Stored(string id) => new(id);

    public static implicit operator Actor(ScribeId scribe) => new(scribe.Value);

    public override string ToString() => Id;
}
