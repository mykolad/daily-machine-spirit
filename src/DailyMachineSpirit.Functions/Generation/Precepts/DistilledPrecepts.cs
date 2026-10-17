using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>The model's answer when distilling the Precepts: its JSON schema is made from this record.</summary>
public sealed record DistilledPrecepts(
    [property: Description("The new precepts, each one short sentence: a rule for writing and choosing rites.")] List<string> Precepts);
