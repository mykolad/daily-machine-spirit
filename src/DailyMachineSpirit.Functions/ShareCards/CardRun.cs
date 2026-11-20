namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>A stretch of a rite's text on a share card: plain words, or code that was between backticks (a chip).</summary>
public sealed record CardRun(string Text, bool IsCode);
