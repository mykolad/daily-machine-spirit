namespace DailyMachineSpirit.Functions.Tags;

/// <param name="Key">The tag in addresses (<c>/tags/version-control</c>).</param>
/// <param name="Label">What readers see ("Version control").</param>
/// <param name="About">What its rites are about, to finish "Rites about …" on its page.</param>
public sealed record Tag(string Key, string Label, string About);
