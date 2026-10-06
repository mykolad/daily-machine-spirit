using System.Net;
using DailyMachineSpirit.Data.Entities;
using Microsoft.Azure.Cosmos;

namespace DailyMachineSpirit.Data.Repositories;

public class ItemRepository : IItemRepository
{
    public const string ContainerName = "items";
    public const string PartitionKeyPath = "/pk";
    /// <summary>
    /// Every document shares one partition. A few hundred small items a year are far below a partition's limits, and one
    /// partition lets a transactional batch save an item and take its number together.
    /// </summary>
    internal const string Partition = "items";
    // A try fails only if another item took the next number in between: that's rare, and retrying is cheap.
    private const int MaxSaveAttempts = 5;

    private static readonly PartitionKey PartitionKey = new(Partition);
    private readonly Container container;

    public ItemRepository(Container container)
    {
        this.container = container;
    }

    public async Task<Item?> GetByNumber(int number, CancellationToken cancellationToken)
        => (await Query<ItemDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", ItemDocument.ItemType)
                .WithParameter("@number", number),
            cancellationToken)).SingleOrDefault()?.ToItem();

    public async Task<Item?> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<ItemDocument>(ItemDocument.IdFor(utcDate), PartitionKey, cancellationToken: cancellationToken);
            return response.Resource.ToItem();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Item>> GetNewest(int count, CancellationToken cancellationToken)
        => (await Query<ItemDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.publishedOnUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", ItemDocument.ItemType),
            cancellationToken)).Select(d => d.ToItem()).ToList();

    public async Task<bool> TryAdd(Item item, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (lastNumber, counterETag) = await ReadLastNumber(cancellationToken);
            var number = lastNumber + 1;
            var counter = new NumberCounterDocument { LastNumber = number };

            var batch = container.CreateTransactionalBatch(PartitionKey);
            batch = counterETag is null
                ? batch.CreateItem(counter)
                : batch.ReplaceItem(NumberCounterDocument.CounterId, counter,
                    new TransactionalBatchItemRequestOptions { IfMatchEtag = counterETag });
            batch = batch.CreateItem(ItemDocument.From(item, number));

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                item.Number = number;
                return true;
            }

            // A batch stops at its first failed operation; the ones after it report 424 (failed dependency).
            var counterStatus = response[0].StatusCode;
            var itemStatus = response[1].StatusCode;
            if (itemStatus == HttpStatusCode.Conflict)
                return false;
            var numberTaken = counterStatus is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
            if (!numberTaken || attempt == MaxSaveAttempts)
                throw new InvalidOperationException(
                    $"Saving the item for {item.PublishedOnUtc:yyyy-MM-dd} failed: {response.StatusCode} (counter {counterStatus}, item {itemStatus}). {response.ErrorMessage}");
        }
    }

    public async Task SaveScores(DateOnly publishedOnUtc, ItemSimilarity similarity, CancellationToken cancellationToken)
    {
        similarity.CreatedAtUtc = Utc.From(similarity.CreatedAtUtc);
        await container.PatchItemAsync<ItemDocument>(ItemDocument.IdFor(publishedOnUtc), PartitionKey,
            [PatchOperation.Set("/similarity", similarity)], cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<int, float[]>> GetScoresByItemNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => (await Query<NumberAndScores>(
            new QueryDefinition("SELECT c.number, c.similarity.scores FROM c WHERE c.type = @type AND c.similarity.scoresGeneratorVersion = @version")
                .WithParameter("@type", ItemDocument.ItemType)
                .WithParameter("@version", scoresGeneratorVersion),
            cancellationToken)).ToDictionary(r => r.Number, r => r.Scores);

    private sealed record NumberAndScores(int Number, float[] Scores);

    private async Task<(int LastNumber, string? ETag)> ReadLastNumber(CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<NumberCounterDocument>(NumberCounterDocument.CounterId, PartitionKey,
                cancellationToken: cancellationToken);
            return (response.Resource.LastNumber, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (0, null);
        }
    }

    private async Task<List<T>> Query<T>(QueryDefinition query, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions { PartitionKey = PartitionKey });
        while (iterator.HasMoreResults)
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        return results;
    }
}
