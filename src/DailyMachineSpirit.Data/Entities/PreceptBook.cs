using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>The Precepts (#12), in the order the Scribes see them, and what the next distillation must know.</summary>
public sealed record PreceptBook
{
    /// <summary>How many precepts there can be: enough to teach, few enough for the writer to keep them all in mind.</summary>
    public const int MaxPrecepts = 12;

    /// <summary>How many deleted precepts are remembered, so a distillation doesn't propose them again.</summary>
    public const int RejectedKept = 30;

    public List<Precept> Precepts { get; init; } = [];

    /// <summary>What the Scribes deleted, the newest first: a distillation is told not to propose these again.</summary>
    public List<string> Rejected { get; init; } = [];

    public Option<DateTime> DistilledAtUtc { get; init; }

    /// <summary>How many of the Scribes' judgments the last distillation learned from.</summary>
    public Option<int> DistilledFrom { get; init; }
}
