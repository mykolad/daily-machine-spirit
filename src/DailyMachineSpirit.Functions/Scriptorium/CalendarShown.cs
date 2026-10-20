using DailyMachineSpirit.Functions.Tags;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// How the calendar is shown: narrowed to one subject (<c>?subject=</c>), and with every rite open (<c>?open=all</c>)
/// rather than the next to publish alone.
/// </summary>
public sealed record CalendarShown(Option<Tag> Subject, bool OpenAll);
