namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The body of <c>POST /api/rites/{number}/vote</c>: exactly "blessed", "heresy", or null to take the vote back. A string,
/// since it's JSON from a browser; the function checks it and turns it into an Option. Required, so a body without it
/// (<c>{}</c>) is refused rather than read as taking the vote back.
/// </summary>
public sealed record VoteRequest
{
    public required string? Vote { get; init; }
}
