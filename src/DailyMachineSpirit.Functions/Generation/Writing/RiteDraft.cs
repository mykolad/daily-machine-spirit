using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>The model's answer: its JSON schema is made from this record, descriptions included.</summary>
public sealed record RiteDraft(
    [property: Description("The rite's name, like \"The Rite of Re-Run\".")] string Title,
    [property: Description("The prayer or ritual itself, in solemn liturgical language.")] string Text,
    [property: Description("What really happens and what would fix it, in plain modern English.")] string HereticalTruth);
