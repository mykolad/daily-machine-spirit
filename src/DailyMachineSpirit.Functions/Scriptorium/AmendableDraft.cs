using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>A waiting draft as the amending page shows it, and its version (ETag), which an amendment must still match.</summary>
public sealed record AmendableDraft(Draft Draft, string ETag);
