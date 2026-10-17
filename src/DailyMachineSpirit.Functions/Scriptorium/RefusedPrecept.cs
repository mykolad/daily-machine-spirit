using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// A Scribe's precept the check refused (#86): the page comes back with their words in the field they used (a new
/// precept's, or the one they edited) and why, so they can reword it.
/// </summary>
public sealed record RefusedPrecept(Option<Guid> PreceptId, string Text, string Reason);
