using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
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

    private static readonly PartitionKey PartitionKey = new(SharedPartition);
    private readonly Container container;
    private readonly ILogger<RiteRepository> logger;

    public RiteRepository(Container container, ILogger<RiteRepository> logger)
    {
        this.container = container;
        this.logger = logger;
    }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            try
            {
                var response = await container.ReadItemAsync<RiteDocument>(RiteDocument.IdFor(utcDate), PartitionKey,
                    cancellationToken: cancellationToken);
                return Some(response.Resource.ToRite());
            }
            catch (CosmosException ex) when (IsMissingItem(ex))
            {
                return Option<Rite>.None;
            }
        });

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Attempt(async () => (await Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.publishedOnUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => Attempt(async () => numbers.Count == 0
            ? []
            : (await Query<RiteDocument>(
                new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@numbers, c.number)")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@numbers", numbers),
                cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => Attempt(async () => (await Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND c.number < @number ORDER BY c.number DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", number),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken)
        => AttemptEither(async () =>
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
                    return Right<Error, Rite>(rite with { Number = number });

                // A batch stops at its first failed operation; the ones after it report 424 (failed dependency).
                var counterStatus = response[0].StatusCode;
                var riteStatus = response[1].StatusCode;
                if (riteStatus == HttpStatusCode.Conflict)
                    return DayAlreadyHasRite;
                var numberTaken = counterStatus is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
                if (!numberTaken || attempt == MaxSaveAttempts)
                    return Error.New(
                        $"Saving the rite for {rite.PublishedOnUtc:yyyy-MM-dd} failed: {response.StatusCode} (counter {counterStatus}, rite {riteStatus}). {response.ErrorMessage}");
                logger.LogInformation("Number {Number} was taken while saving the rite for {Day}; trying the next one.",
                    number, rite.PublishedOnUtc);
            }
        });

    public Task<Either<Error, Option<ReactionCounts>>> React(
        int number, Option<Reaction> reaction, Option<Reaction> previous, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            var found = (await Query<RiteDocument>(
                new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken)).HeadOrNone();
            return await found.Match(
                Some: async document => Some(await MoveReaction(document, reaction, previous, cancellationToken)),
                None: () => Task.FromResult(Option<ReactionCounts>.None));
        });

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(publishedOnUtc), PartitionKey,
                [PatchOperation.Set("/similarity", Utc.From(similarity))], cancellationToken: cancellationToken);
            return unit;
        });

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Attempt(async () => (await Query<NumberAndScores>(
            new QueryDefinition("SELECT c.number, c.similarity.scores FROM c WHERE c.type = @type AND c.similarity.scoresGeneratorVersion = @version")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@version", scoresGeneratorVersion),
            cancellationToken)).ToDictionary(r => r.Number, r => r.Scores));

    private sealed record NumberAndScores(int Number, float[] Scores);

    // Any Cosmos call can fail (network, throttling, a missing role, an unreadable document): callers get that as an Error
    // to handle, with the exception inside for the logs. Cancellation still throws, as everywhere in .NET.
    private static Task<Either<Error, T>> Attempt<T>(Func<Task<T>> operation)
        => AttemptEither(async () => Right<Error, T>(await operation()));

    private static async Task<Either<Error, T>> AttemptEither<T>(Func<Task<Either<Error, T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.New(ex);
        }
    }

    // A missing document is 404 with sub-status 0. A missing container or database is also 404 (sub-status 1003), but
    // that's an outage, which must reach the caller as an error rather than as "nothing there".
    private static bool IsMissingItem(CosmosException ex) => ex is { StatusCode: HttpStatusCode.NotFound, SubStatusCode: 0 };

    private async Task<(int LastNumber, Option<string> ETag)> ReadLastNumber(CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<NumberCounterDocument>(NumberCounterDocument.CounterId, PartitionKey,
                cancellationToken: cancellationToken);
            return (response.Resource.LastNumber, response.ETag);
        }
        catch (CosmosException ex) when (IsMissingItem(ex))
        {
            return (0, None);
        }
    }

    // Increments rather than read-modify-write, so concurrent reactions never overwrite each other. Taking back a
    // reaction only applies while its count is above zero; if it's already zero (a browser claiming a reaction that was
    // never counted), only the new reaction counts.
    private async Task<ReactionCounts> MoveReaction(
        RiteDocument document, Option<Reaction> reaction, Option<Reaction> previous, CancellationToken cancellationToken)
    {
        if (reaction == previous)
            return new ReactionCounts(document.BlessedCount, document.HeresyCount);

        var add = reaction.Map(r => PatchOperation.Increment($"/{CountField(r)}", 1)).ToList();
        var takeBack = previous.Map(p => PatchOperation.Increment($"/{CountField(p)}", -1)).ToList();
        try
        {
            return await PatchCounts(document.Id, [.. add, .. takeBack],
                previous.Map(p => $"FROM c WHERE c.{CountField(p)} > 0"), cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            if (add.Count > 0)
                return await PatchCounts(document.Id, add, None, cancellationToken);
            // Nothing to change, but the counts may have moved since the query: answer what's stored now.
            var current = (await container.ReadItemAsync<RiteDocument>(document.Id, PartitionKey, cancellationToken: cancellationToken)).Resource;
            return new ReactionCounts(current.BlessedCount, current.HeresyCount);
        }
    }

    private async Task<ReactionCounts> PatchCounts(
        string id, IReadOnlyList<PatchOperation> operations, Option<string> filter, CancellationToken cancellationToken)
    {
        var options = new PatchItemRequestOptions { FilterPredicate = filter.IfNoneUnsafe((string?)null) };
        var patched = (await container.PatchItemAsync<RiteDocument>(id, PartitionKey, operations, options, cancellationToken)).Resource;
        return new ReactionCounts(patched.BlessedCount, patched.HeresyCount);
    }

    private static string CountField(Reaction reaction) => reaction == Reaction.Blessed ? "blessedCount" : "heresyCount";

    private async Task<List<T>> Query<T>(QueryDefinition query, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions { PartitionKey = PartitionKey });
        while (iterator.HasMoreResults)
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        return results;
    }
}
