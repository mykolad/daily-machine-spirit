using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>The model's answer to which precepts break the writer's rules: its JSON schema is made from this record.</summary>
public sealed record PreceptCheckAnswer(
    [property: Description("The precepts that break a rule or are abusive, by number; empty when none does.")] List<BrokenPrecept> Breaking);
