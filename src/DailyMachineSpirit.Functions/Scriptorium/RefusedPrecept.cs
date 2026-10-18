using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// A Scribe's precept the check refused, or couldn't check (#86): the page comes back with their words in the field they
/// used (a new precept's, or the one they edited), why, and what they can do: reword it, or try again.
/// </summary>
public sealed record RefusedPrecept(Option<Guid> PreceptId, string Text, string Reason, string NextStep);
