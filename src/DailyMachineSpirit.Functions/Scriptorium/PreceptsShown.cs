using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// How the Precepts' page is shown: the precept whose words are open to edit (<c>?edit=</c>, the way without the
/// script), the time (for the next distillation), who's looking, and the Scribes' names, to say who pinned or wrote each.
/// </summary>
public sealed record PreceptsShown(Option<Guid> Editing, DateTime NowUtc, Option<NameInTheRecord> Name, IReadOnlyList<ScribeName> Names);
