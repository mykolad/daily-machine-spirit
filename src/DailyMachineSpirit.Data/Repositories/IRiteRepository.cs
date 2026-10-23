using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites in Cosmos DB, read and saved: what publishing, refilling, the Scribes and the backfills take, since what they
/// read leads to what they save. Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IRiteRepository : IRiteReader
{
    /// <summary>
    /// The saved rite, with the next number; <see cref="RiteRepository.DayAlreadyHasRite"/> when that day already has one,
    /// so two generations for the same day can't both save.
    /// </summary>
    Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a waiting draft as that day's rite, with the next number, and marks the draft published, all together.
    /// <see cref="RiteRepository.DayAlreadyHasRite"/> when the day has a rite; <see cref="RiteRepository.DraftNotWaiting"/>
    /// when the draft was published (or changed) meanwhile.
    /// </summary>
    Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken);

    /// <summary>Replaces any older scores of the rite.</summary>
    Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken);

    /// <summary>The rites judged before tags existed: with scores, without tags.</summary>
    Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken);
}
