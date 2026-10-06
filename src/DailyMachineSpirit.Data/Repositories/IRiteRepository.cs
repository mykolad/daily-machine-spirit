using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

public interface IRiteRepository
{
    Task<Rite?> GetByNumber(int number, CancellationToken cancellationToken);
    Task<Rite?> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);
    Task<List<Rite>> GetNewest(int count, CancellationToken cancellationToken);
    /// <summary>
    /// Saves the rite with the next number. False when that day already has a rite: two generations for the same day
    /// can't both save.
    /// </summary>
    Task<bool> TryAdd(Rite rite, CancellationToken cancellationToken);
    /// <summary>Replaces any older scores of the rite.</summary>
    Task SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken);
    Task<Dictionary<int, float[]>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
