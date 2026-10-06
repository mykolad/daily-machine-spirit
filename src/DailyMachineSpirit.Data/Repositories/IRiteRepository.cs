using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

public interface IRiteRepository
{
    Task<Option<Rite>> GetByNumber(int number, CancellationToken cancellationToken);
    Task<Option<Rite>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);
    Task<List<Rite>> GetNewest(int count, CancellationToken cancellationToken);
    /// <summary>
    /// The saved rite, with the next number; or <see cref="RiteRepository.DayAlreadyHasRite"/>, so two generations for
    /// the same day can't both save.
    /// </summary>
    Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken);
    /// <summary>Replaces any older scores of the rite.</summary>
    Task SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken);
    Task<Dictionary<int, float[]>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
