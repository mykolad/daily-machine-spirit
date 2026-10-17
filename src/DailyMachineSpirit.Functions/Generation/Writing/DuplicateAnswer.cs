using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>The model's answer to whether a new rite repeats one already written: its JSON schema is made from this.</summary>
public sealed record DuplicateAnswer(
    [property: Description("The number of the existing rite the new one repeats, or 0 if it repeats none.")] int SameAs);
