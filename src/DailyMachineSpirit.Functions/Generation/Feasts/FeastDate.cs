namespace DailyMachineSpirit.Functions.Generation.Feasts;

/// <summary>A feast, and the day it falls on that a draft is written for.</summary>
public sealed record FeastDate(Feast Feast, DateOnly Day);
