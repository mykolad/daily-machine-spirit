using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// False truths written for a published rite from before them (#199), waiting for a Scribe: shown with the rite's own
/// words they'll be read beside, so the Scribe can judge them, and checked with them when accepted. A rite from before
/// ailments (#115) has none.
/// </summary>
public sealed record ProposedFalseTruths(int Number, RiteKind Kind, string Title, string Text, string HereticalTruth, Option<string> Ailment, FalseTruths Proposed);
