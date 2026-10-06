using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

public interface IItemRepository
{
    Task<Item?> GetById(int id, CancellationToken cancellationToken);
    Task<Item?> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);
    Task<List<Item>> GetNewest(int count, CancellationToken cancellationToken);
    /// <summary>False when that day already has an item: two generations for the same day can't both save.</summary>
    Task<bool> TryAdd(Item item, CancellationToken cancellationToken);
    /// <summary>Replaces any older scores of the item.</summary>
    Task SaveScores(int itemId, string scoresGeneratorVersion, float[] scores, DateTime createdAtUtc, CancellationToken cancellationToken);
    Task<Dictionary<int, float[]>> GetScoresByItemId(string scoresGeneratorVersion, CancellationToken cancellationToken);
}
