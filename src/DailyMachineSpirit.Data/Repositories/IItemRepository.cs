using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

public interface IItemRepository
{
    Task<Item?> GetByNumber(int number, CancellationToken cancellationToken);
    Task<Item?> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);
    Task<List<Item>> GetNewest(int count, CancellationToken cancellationToken);
    /// <summary>
    /// Saves the item with the next number. False when that day already has an item: two generations for the same day
    /// can't both save.
    /// </summary>
    Task<bool> TryAdd(Item item, CancellationToken cancellationToken);
    /// <summary>Replaces any older scores of the item.</summary>
    Task SaveScores(DateOnly publishedOnUtc, ItemSimilarity similarity, CancellationToken cancellationToken);
    Task<Dictionary<int, float[]>> GetScoresByItemNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
