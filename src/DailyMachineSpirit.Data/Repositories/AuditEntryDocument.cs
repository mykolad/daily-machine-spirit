using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>An <see cref="AuditEntry"/> as stored, in the rites' partition, so it joins the batch of the change it records.</summary>
internal sealed record AuditEntryDocument
{
    public const string AuditType = "audit";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = AuditType;

    public DateTime AtUtc { get; init; }

    public string Actor { get; init; } = string.Empty;

    public AuditAction Action { get; init; }

    public Guid? DraftId { get; init; }

    public int? RiteNumber { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public static AuditEntryDocument From(AuditEntry entry) => new()
    {
        Id = entry.Id.ToString("D"),
        AtUtc = Utc.From(entry.AtUtc),
        Actor = entry.Actor,
        Action = entry.Action,
        DraftId = entry.DraftId.OrNullable(),
        RiteNumber = entry.RiteNumber.OrNullable(),
        Title = entry.Title,
        Detail = entry.Detail.OrNull(),
    };

    public AuditEntry ToEntry() => new()
    {
        Id = Guid.Parse(Id),
        AtUtc = AtUtc,
        Actor = Actor,
        Action = Action,
        DraftId = Optional(DraftId),
        RiteNumber = Optional(RiteNumber),
        Title = Title,
        Detail = Optional(Detail),
    };
}
