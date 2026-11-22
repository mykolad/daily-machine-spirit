using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>A verdict alone, as most tests vote: set, switched or taken back, the reader's confession left as it is.</summary>
internal static class VerdictVotes
{
    public static Task<Either<Error, Option<SavedVote>>> Vote(
        this IVoteRepository votes, int riteNumber, VoterKey voterKey, Option<Reaction> verdict, CancellationToken cancellationToken)
        => votes.Vote(riteNumber, voterKey, vote => vote with { Reaction = verdict }, cancellationToken);
}
