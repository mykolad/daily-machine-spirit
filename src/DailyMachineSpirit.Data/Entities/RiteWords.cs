using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// What a search reads of a rite, and where it stands: its words and its subjects' tags, without its scores or votes. A
/// small part of each rite's document. Its form too (<see cref="RiteWriting.Form"/>), which the writer is asked to vary.
/// </summary>
public sealed record RiteWords(
    int Number, DateOnly PublishedOnUtc, RiteKind Kind, string Title, string Text, string HereticalTruth, Option<string> Ailment, IReadOnlyList<string> Tags, Option<string> Form);
