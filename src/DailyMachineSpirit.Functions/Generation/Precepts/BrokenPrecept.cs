using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>One precept the check refuses, as the model answers it.</summary>
public sealed record BrokenPrecept(
    [property: Description("The precept's number.")] int Number,
    [property: Description("Which rule it breaks, or why it's abusive, in one short sentence a moderator can act on.")] string Reason);
