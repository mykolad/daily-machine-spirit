using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The body of <c>POST /api/rites/{number}/reaction</c>: the visitor's reaction now and the one their browser
/// remembered, each "blessed", "heresy" or null. Nullable, since it's JSON from a browser; the function turns them
/// into Options.
/// </summary>
public sealed record ReactionRequest
{
    public Reaction? Reaction { get; init; }

    public Reaction? Previous { get; init; }
}
