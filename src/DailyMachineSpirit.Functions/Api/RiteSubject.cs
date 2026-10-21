namespace DailyMachineSpirit.Functions.Api;

/// <summary>A subject a rite is filed under, from the site's own vocabulary.</summary>
/// <param name="Key">"caches": the subject's page is <c>/tags/caches</c>.</param>
/// <param name="Label">"Caches".</param>
public sealed record RiteSubject(string Key, string Label);
