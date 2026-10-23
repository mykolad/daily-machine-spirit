namespace DailyMachineSpirit.Data.Entities;

/// <summary>The precept a Scribe deleted last, and its place (0 is first): what an Undo puts back.</summary>
public sealed record DeletedPrecept(Precept Precept, int Place);
