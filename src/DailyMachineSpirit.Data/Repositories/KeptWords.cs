using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Every stored copy of a published rite's words, its own first, and the draft it was published from: an erasure's
/// Record entry names that draft, so the draft's history shows it. None for a rite written on the spot.
/// </summary>
public sealed record KeptWords(Option<Guid> DraftId, List<WordsKept> Copies);
