using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>An amendment the Scribe can fix on the page: the message says what to change.</summary>
public sealed record AmendmentRefused(string Reason) : Expected(Reason, 0, default);
