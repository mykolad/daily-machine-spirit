namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A vote as saved: the rite's counts after it, and the reader's say before and after. The same vote sent again, or
/// taking back a vote that was never cast, changes nothing: the counts still come back, but there's no vote to count and
/// no page to refresh.
/// </summary>
public sealed record SavedVote(ReactionCounts Counts, ReaderVote Before, ReaderVote After)
{
    public bool Changed => Before != After;
}
