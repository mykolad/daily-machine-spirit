using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;

namespace DailyMachineSpirit.Tests;

/// <summary>The whole Record as its page shows it, unnarrowed, for tests that read what its entries say.</summary>
public static class RecordPages
{
    public static string Of(IReadOnlyList<AuditEntry> entries, IReadOnlyList<ScribeName> names)
        => ScriptoriumPage.RenderRecord(new RecordShown(
            entries, names, Option<TabCounts>.None, Option<Functions.Scriptorium.Naming.NameInTheRecord>.None, Option<Guid>.None,
            Option<Draft>.None, RecordFilter.Everything, Option<DateOnly>.None));
}
