using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The Scribes' order of the drafts, as read, and who placed each (their Access id): <see cref="ETag"/> lets saving it
/// detect another Scribe's change. A draft placed before the order kept its Scribes has none in
/// <see cref="PlacedBy"/>.
/// </summary>
public sealed record Placements(List<Guid> Order, IReadOnlyDictionary<Guid, ScribeId> PlacedBy, Option<string> ETag);
