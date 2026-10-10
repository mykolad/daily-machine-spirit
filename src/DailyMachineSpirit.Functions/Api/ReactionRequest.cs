namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The body of <c>POST /api/rites/{number}/reaction</c>: the visitor's reaction now and the one their browser
/// remembered, each exactly "blessed", "heresy" or null. Strings, since it's JSON from a browser; the function checks
/// them and turns them into Options.
/// </summary>
public sealed record ReactionRequest
{
    public string? Reaction { get; init; }

    public string? Previous { get; init; }
}
