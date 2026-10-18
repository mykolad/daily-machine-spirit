namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>The Scribes' pages (<c>machine-spirit.jsonc</c>, <c>ScriptoriumPages</c>).</summary>
public sealed record ScriptoriumPagesOptions
{
    public const string SectionName = "ScriptoriumPages";

    public int AshesShown { get; init; }

    public int JudgmentsShown { get; init; }

    public int RecordEntriesShown { get; init; }

    public int AgreementMonthsShown { get; init; }
}
