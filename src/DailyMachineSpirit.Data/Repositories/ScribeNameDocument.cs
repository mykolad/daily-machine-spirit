using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>One Scribe's name as stored: the Access id a plain string, as it always was.</summary>
internal sealed record ScribeNameDocument
{
    public string AccessId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public static ScribeNameDocument From(ScribeName name) => new() { AccessId = name.Scribe.Value, Name = name.Name };

    public ScribeName ToName() => new(new ScribeId(AccessId), Name);
}
