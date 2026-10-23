using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>The model's answer when asked for an existing rite's ailment: its JSON schema is made from this.</summary>
public sealed record AilmentAnswer(
    [property: Description("The trouble the rite is for, as a developer would describe it, without its cause or fix.")] string Ailment);
