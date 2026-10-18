using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>A Scribe's precept refused for breaking the writer's rules (#86): the message is the check's reason.</summary>
public sealed record BreaksARule(string Reason) : Expected(Reason, 0, default);
