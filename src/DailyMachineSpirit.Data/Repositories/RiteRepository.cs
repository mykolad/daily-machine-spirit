using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public class RiteRepository : IRiteRepository
{
    public const string ContainerName = "rites";
    public const string PartitionKeyPath = "/partition";
    /// <summary>
    /// Every document shares one partition. A few hundred small rites a year are far below a partition's limits, and one
    /// partition lets a transactional batch save a rite and take its number together.
    /// </summary>
    internal const string SharedPartition = "rites";
    // A try fails only if another rite took the next number in between: that's rare, and retrying is cheap.
    private const int MaxSaveAttempts = 5;

    public static readonly Error DayAlreadyHasRite = Error.New("That day already has a rite.");

    private static readonly PartitionKey PartitionKey = new(Partition);
    private readonly Container container;

    public RiteRepository(Container container)
    {
        this.container = container;
    }

    public async Task<Option<Rite>> GetByNumber(int number, CancellationToken cancellationToken)
        => (await Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", number),
            cancellationToken)).HeadOrNone().Map(document => document.ToRite());

    public async Task<Option<Rite>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<RiteDocument>(RiteDocument.IdFor(utcDate), PartitionKey, cancellationToken: cancellationToken);
            return response.Resource.ToRite();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return None;
        }
    }

    public async Task<List<Rite>> GetNewest(int count, CancellationToken cancellationToken)
        => (await Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.publishedOnUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList();

    public async Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (lastNumber, counterETag) = await ReadLastNumber(cancellationToken);
            var number = lastNumber + 1;
            var counter = new NumberCounterDocument { LastNumber = number };

            var batch = container.CreateTransactionalBatch(PartitionKey);
            batch = counterETag.Match(
                Some: etag => batch.ReplaceItem(NumberCounterDocument.CounterId, counter,
                    new TransactionalBatchItemRequestOptions { IfMatchEtag = etag }),
                None: () => batch.CreateItem(counter));
            batch = batch.CreateItem(RiteDocument.From(rite, number));

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                return rite with { Number = number };

            // A batch stops at its first failed operation; the ones after it report 424 (failed dependency).
            var counterStatus = response[0].StatusCode;
            var riteStatus = response[1].StatusCode;
            if (riteStatus == HttpStatusCode.Conflict)
                return DayAlreadyHasRite;
            var numberTaken = counterStatus is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
            if (!numberTaken || attempt == MaxSaveAttempts)
                throw new InvalidOperationException(
                    $"Saving the rite for {rite.PublishedOnUtc:yyyy-MM-dd} failed: {response.StatusCode} (counter {counterStatus}, rite {riteStatus}). {response.ErrorMessage}");
        }
    }

    public async Task SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
    {
        await container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(publishedOnUtc), PartitionKey,
            [PatchOperation.Set("/similarity", Utc.From(similarity))], cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<int, float[]>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => (await Query<NumberAndScores>(
            new QueryDefinition("SELECT c.number, c.similarity.scores FROM c WHERE c.type = @type AND c.similarity.scoresGeneratorVersion = @version")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@version", scoresGeneratorVersion),
            cancellationToken)).ToDictionary(r => r.Number, r => r.Scores);

    private sealed record NumberAndScores(int Number, float[] Scores);

    private async Task<(int LastNumber, Option<string> ETag)> ReadLastNumber(CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<NumberCounterDocument>(NumberCounterDocument.CounterId, PartitionKey,
                cancellationToken: cancellationToken);
            return (response.Resource.LastNumber, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (0, None);
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
