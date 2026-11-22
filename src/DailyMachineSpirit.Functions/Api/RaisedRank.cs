namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The office a vote raised the reader to (#201), in the toast's own words, for the page to show as it is: "Thou art now
/// an Acolyte.", "15 more rites to Lector.", and the office's insignia.
/// </summary>
public sealed record RaisedRank(string Office, string Next, string Insignia);
