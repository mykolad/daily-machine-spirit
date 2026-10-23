using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>What the Record is narrowed to, from its address (<c>?who=scribes&amp;what=moved</c>).</summary>
public sealed record RecordFilter(RecordActors Who, Option<RecordKind> What)
{
    public static RecordFilter Everything { get; } = new(RecordActors.Everyone, Option<RecordKind>.None);
}
