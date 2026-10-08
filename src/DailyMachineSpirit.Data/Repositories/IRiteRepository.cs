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

    /// <summary>The rites numbered below <paramref name="number"/>, the newest first: a page of the archive.</summary>
    Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken);

    /// <summary>
    /// The saved rite, with the next number; <see cref="RiteRepository.DayAlreadyHasRite"/> when that day already has one,
    /// so two generations for the same day can't both save.
    /// </summary>
    Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken);

    /// <summary>
    /// Moves one visitor's reaction from <paramref name="previous"/> to <paramref name="reaction"/> (either may be
    /// None) and returns the counts after it, or None when there's no such rite. Reactions are anonymous: the visitor's
    /// browser remembers its own and sends it as <paramref name="previous"/>, and nothing about the visitor is stored.
    /// A count never drops below zero, whatever a browser claims.
    /// </summary>
    Task<Either<Error, Option<ReactionCounts>>> React(
        int number, Option<Reaction> reaction, Option<Reaction> previous, CancellationToken cancellationToken);

    /// <summary>Replaces any older scores of the rite.</summary>
    Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken);

    Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
