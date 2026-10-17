using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The Scribes' order of the drafts, as read: <see cref="ETag"/> lets saving it detect another Scribe's change.</summary>
public sealed record Placements(List<Guid> Order, Option<string> ETag);
