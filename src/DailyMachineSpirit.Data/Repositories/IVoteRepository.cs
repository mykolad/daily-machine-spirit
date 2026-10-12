using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Signed-in readers' votes, one per account per rite, and the rites' counts that follow them.</summary>
public interface IVoteRepository
{
    /// <summary>
    /// Sets the reader's vote on a rite, switches it, or takes it back (None), and moves the rite's counts in the same
    /// batch. Answers the counts after it, or None when there's no such rite.
    /// </summary>
    Task<Either<Error, Option<ReactionCounts>>> Vote(int riteNumber, string voterKey, Option<Reaction> vote, CancellationToken cancellationToken);

    /// <summary>The reader's votes among these rites, by rite number; a rite they haven't voted on isn't in it.</summary>
    Task<Either<Error, Dictionary<int, Reaction>>> GetVotes(string voterKey, IReadOnlyCollection<int> riteNumbers, CancellationToken cancellationToken);
}
