using System.Text.Json;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The body of <c>POST /api/rites/{number}/vote</c>: what the reader changes, each left as it was when absent. The vote
/// is exactly "blessed", "heresy", or null to take it back; confessed is true or false (#200). Both are
/// <see cref="JsonElement"/>s, so an absent one (Undefined) is told from null, and the function checks them and turns
/// them into Options. A body asking for neither (<c>{}</c>) is refused rather than read as changing nothing.
/// </summary>
public sealed record VoteRequest
{
    public JsonElement Vote { get; init; }

    public JsonElement Confessed { get; init; }
}
