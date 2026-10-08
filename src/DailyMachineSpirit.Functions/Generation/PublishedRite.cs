using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>The day's rite, and whether this run published it (or found it already there).</summary>
public sealed record PublishedRite(Rite Rite, bool IsNew);
