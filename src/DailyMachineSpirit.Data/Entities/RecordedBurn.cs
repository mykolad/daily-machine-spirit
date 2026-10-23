using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A draft's latest burn as the Record and the Scribes' decisions tell it: who burned it (the Record's entry), from
/// which place and why (the decision). Each is None where neither has it: a burn older than the Record, or one
/// without a note.
/// </summary>
public sealed record RecordedBurn(Option<string> By, Option<int> FromPlace, Option<string> Reason);
