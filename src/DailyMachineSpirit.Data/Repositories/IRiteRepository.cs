using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Every call returns its failure as an <see cref="Error"/> (Cosmos down, throttled, unreadable data), never throws one.</summary>
public interface IRiteRepository
{
    Task<Either<Error, Option<Rite>>> GetByNumber(int number, CancellationToken cancellationToken);

    Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);

    Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken);

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

    Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
