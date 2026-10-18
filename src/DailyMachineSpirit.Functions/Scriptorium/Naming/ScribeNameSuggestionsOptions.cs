using DailyMachineSpirit.Functions.Generation;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>The names suggested to a Scribe (<c>machine-spirit.jsonc</c>, <c>ScribeNameSuggestions</c>).</summary>
public sealed record ScribeNameSuggestionsOptions
{
    public const string SectionName = "ScribeNameSuggestions";

    public string Model { get; init; } = string.Empty;

    /// <summary>How many the page offers at once.</summary>
    public int Count { get; init; }

    public Patience Patience { get; init; } = new();
}
