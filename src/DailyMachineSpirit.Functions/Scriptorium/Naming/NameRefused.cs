using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>A name the Scribe can't take: the message says why, for them to choose another.</summary>
public sealed record NameRefused(string Reason) : Expected(Reason, 0, default);
