namespace DailyMachineSpirit.Data.Entities;

/// <summary>The rites' snapshot as saved (#112): every published rite, and the copy's version.</summary>
public sealed record RiteSnapshot(List<Rite> Rites, string Version);
