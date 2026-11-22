using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Signed-in readers' votes, one per account per rite: a verdict and a confession (#200), and the rites' counts that
/// follow them.
/// </summary>
public interface IVoteRepository
{
    /// <summary>
    /// Changes the reader's vote on a rite as asked (<paramref name="change"/>, given their vote as it is: a verdict set,
    /// switched or taken back, a confession made or withdrawn), and moves the rite's counts in the same batch. Answers
    /// the counts after it and the vote before and after, or None when there's no such rite.
    /// </summary>
    Task<Either<Error, Option<SavedVote>>> Vote(int riteNumber, VoterKey voterKey, Func<ReaderVote, ReaderVote> change, CancellationToken cancellationToken);

    /// <summary>The reader's votes among these rites, by rite number; a rite they have no say on isn't in it.</summary>
    Task<Either<Error, Dictionary<int, ReaderVote>>> GetVotes(VoterKey voterKey, IReadOnlyCollection<int> riteNumbers, CancellationToken cancellationToken);

    /// <summary>Every vote the reader has, by rite number.</summary>
    Task<Either<Error, Dictionary<int, ReaderVote>>> GetAllVotes(VoterKey voterKey, CancellationToken cancellationToken);

    /// <summary>How many rites the reader has a say on (#201): voted on or confessed to, each once.</summary>
    Task<Either<Error, int>> CountRites(VoterKey voterKey, CancellationToken cancellationToken);
}
