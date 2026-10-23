using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// The precept a Scribe deleted last, and its place (0 is first): what an Undo puts back. When the deleted precepts
/// remembered were already as many as are kept, the delete let the oldest go (<see cref="LetGo"/>), and an Undo
/// remembers it again.
/// </summary>
public sealed record DeletedPrecept(Precept Precept, int Place, Option<string> LetGo);
