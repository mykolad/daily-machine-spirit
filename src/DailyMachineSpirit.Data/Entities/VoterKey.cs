namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A signed-in reader, as far as the shrine knows them: a one-way key made from their account at a sign-in provider,
/// which never leads back to it. All that's kept with their votes and the rites they've witnessed.
/// </summary>
public sealed record VoterKey(string Value);
