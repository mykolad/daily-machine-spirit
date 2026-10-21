namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// A published rite as the rites API (version 1) lists it, its Heretical Truth still sealed: a contract with clients
/// outside the site, so its names and meaning stay as they are, whatever the site's own model becomes.
/// </summary>
/// <param name="Day">The day it was published, "2026-10-14".</param>
/// <param name="Kind">"prayer" or "ritual".</param>
/// <param name="Text">As written, commands and file names in `backticks`.</param>
/// <param name="Ailment">
/// The trouble the rite is for, in a line, without its fix: what a client matches a user's trouble on. Optional, and
/// empty until rites have one.
/// </param>
/// <param name="Url">Its page, "https://dailymachinespirit.fyi/r/12".</param>
public sealed record PublishedRite(
    int Number, string Day, string Kind, string Title, string Text, string Ailment, IReadOnlyList<RiteSubject> Subjects, string Url);
