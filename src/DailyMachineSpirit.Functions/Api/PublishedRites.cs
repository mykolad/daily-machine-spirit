namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The rites API's answer (version 1): every published rite, the newest first. An object rather than a bare list, so
/// fields can be added beside the rites without breaking a client.
/// </summary>
public sealed record PublishedRites(IReadOnlyList<PublishedRite> Rites);
