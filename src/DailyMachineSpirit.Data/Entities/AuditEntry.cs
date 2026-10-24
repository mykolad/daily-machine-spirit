using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// One change to the drafts or the rites, and who made it (#9): every Scribe action, and what the Machine Spirit does
/// on its own (writing drafts, publishing). Append-only, saved with the change it records, and kept for as long as the
/// drafts and rites are: burned drafts stay, hidden, so an entry can always point at its draft.
/// </summary>
public sealed record AuditEntry
{
    public Guid Id { get; init; }

    public DateTime AtUtc { get; init; }

    public required Actor Actor { get; init; }

    public AuditAction Action { get; init; }

    /// <summary>The draft it was about, if any (a summon or the Augury's order is about none).</summary>
    public Option<Guid> DraftId { get; init; }

    /// <summary>The rite's number, once it was published.</summary>
    public Option<int> RiteNumber { get; init; }

    /// <summary>The draft's or rite's title at the time, so the log reads on its own.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What changed, where it says more than the action: "#3 to #1", the title it had before an amendment.</summary>
    public Option<string> Detail { get; init; }
}
