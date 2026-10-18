using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>The model's answer when asked for names a Scribe might take: its JSON schema is made from this record.</summary>
public sealed record SuggestedNames(
    [property: Description("Three names a moderator might take, each a title and one word, e.g. \"Brother Ferrum\".")] List<string> Names);
