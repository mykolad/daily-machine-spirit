namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A rite's or a draft's words written before rites had an ailment, as they were read: what an ailment is written from.
/// The version is the document's, so the ailment is saved only to the words it was written from.
/// </summary>
/// <param name="Published">A published rite, whose page and the rites API show its words; else a draft.</param>
public sealed record WordsWithoutAilment(
    string DocumentId, string Version, bool Published, RiteKind Kind, string Title, string Text, string HereticalTruth);
