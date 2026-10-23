using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public class RiteRepository : IRiteRepository
{
    public const string ContainerName = "rites";
    public const string PartitionKeyPath = "/partition";
    // A try fails only if another rite took the next number in between: that's rare, and retrying is cheap.
    private const int MaxSaveAttempts = 5;

    public static readonly Error DayAlreadyHasRite = Error.New("That day already has a rite.");
    public static readonly Error DraftNotWaiting = Error.New("That draft isn't waiting any more: it was published or changed meanwhile.");

    private readonly Container container;
    private readonly ILogger<RiteRepository> logger;

    public RiteRepository(Container container, ILogger<RiteRepository> logger)
    {
        this.container = container;
        this.logger = logger;
    }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<RiteDocument>(RiteDocument.IdFor(utcDate), cancellationToken))
            .Map(found => found.Document.ToRite()));

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.publishedOnUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type ORDER BY c.number DESC")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => Attempt(async () => numbers.Count == 0
            ? []
            : (await container.Query<RiteDocument>(
                new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@numbers, c.number)")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@numbers", numbers),
                cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND c.number < @number ORDER BY c.number DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", number),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            Holding(words, tag, "SELECT TOP @count * FROM c", "AND c.number < @number ORDER BY c.number DESC")
                .WithParameter("@count", count)
                .WithParameter("@number", number),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<FoundRiteDocument>(
            Holding(words, tag, "SELECT c.number, c.publishedOnUtc, c.tags FROM c", "ORDER BY c.number DESC"),
            cancellationToken)).Select(document => document.ToFoundRite()).ToList());

    // Only the words and where each rite stands: the scores alone would be most of every document.
    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken)
        => Attempt(async () => await container.Query<RiteWords>(
            new QueryDefinition("SELECT c.number, c.publishedOnUtc, c.kind, c.title, c.text, c.hereticalTruth FROM c WHERE c.type = @type ORDER BY c.number DESC")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken));

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<UnrevealedRiteDocument>(
            new QueryDefinition(
                "SELECT c.number, c.publishedOnUtc, c.kind, c.title, c.text, c.tags FROM c WHERE c.type = @type ORDER BY c.number DESC")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToUnrevealedRite()).ToList());

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<string>(
            new QueryDefinition("SELECT VALUE c.hereticalTruth FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", number),
            cancellationToken)).HeadOrNone());

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken)
        => Attempt(async () => await container.Query<RiteDay>(
            new QueryDefinition("SELECT c.number, c.publishedOnUtc FROM c WHERE c.type = @type ORDER BY c.number DESC")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken));

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken)
        => AttemptEither(() => Save(rite, None, cancellationToken));

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            // The stored draft, not the caller's copy: a moderator may have changed it since the caller read it.
            var stored = await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken);
            return await stored
                .Filter(found => found.Document.State == DraftState.Waiting)
                .Match(
                    Some: found => Save(
                        found.Document.ToDraft().ToRite(publishedOnUtc),
                        Some((found.Document with { State = DraftState.Published, PublishedOnUtc = publishedOnUtc }, found.ETag)),
                        cancellationToken),
                    None: () => Task.FromResult(Left<Error, Rite>(DraftNotWaiting)));
        });

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(publishedOnUtc), SharedPartitionKey,
                [PatchOperation.Set("/similarity", Utc.From(similarity))], cancellationToken: cancellationToken);
            return unit;
        });

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<NumberAndScores>(
            new QueryDefinition("SELECT c.number, c.similarity.scores FROM c WHERE c.type = @type AND c.similarity.scoresGeneratorVersion = @version")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@version", scoresGeneratorVersion),
            cancellationToken)).ToDictionary(r => r.Number, r => r.Scores));

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(c.tags, @tag) AND c.number < @number ORDER BY c.number DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@tag", tag)
                .WithParameter("@number", number),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND IS_OBJECT(c.similarity) AND NOT IS_ARRAY(c.tags)")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList());

    public Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(publishedOnUtc), SharedPartitionKey,
                [PatchOperation.Set("/tags", tags)], cancellationToken: cancellationToken);
            return unit;
        });

    private sealed record NumberAndScores(int Number, float[] Scores);

    // The rites holding every word somewhere in their title, text or Heretical Truth, ignoring case. Each word is a
    // parameter, so what a reader typed never becomes part of the query's text. A plain CONTAINS reads every rite, which
    // costs little at a few hundred; Cosmos's full-text index can take over if that grows.
    private static QueryDefinition Holding(IReadOnlyList<string> words, Option<string> tag, string select, string rest)
    {
        var conditions = words.Select((_, index) =>
            $" AND (CONTAINS(c.title, @word{index}, true) OR CONTAINS(c.text, @word{index}, true) OR CONTAINS(c.hereticalTruth, @word{index}, true))");
        var filed = tag.IsSome ? " AND ARRAY_CONTAINS(c.tags, @tag)" : string.Empty;
        var query = new QueryDefinition($"{select} WHERE c.type = @type{string.Concat(conditions)}{filed} {rest}")
            .WithParameter("@type", RiteDocument.RiteType);
        var filedUnder = tag.Match(Some: key => query.WithParameter("@tag", key), None: () => query);
        return words.Select((word, index) => (word, index))
            .Aggregate(filedUnder, (holding, word) => holding.WithParameter($"@word{word.index}", word.word));
    }

    // Saves the rite and takes its number in one batch; when it comes from a draft, the draft is marked published in the
    // same batch, and only if nobody changed it since it was read (its ETag), so a draft is never published twice.
    private async Task<Either<Error, Rite>> Save(
        Rite rite, Option<(DraftDocument Published, string ETag)> fromDraft, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (lastNumber, counterETag) = await ReadLastNumber(cancellationToken);
            var number = lastNumber + 1;
            var counter = new NumberCounterDocument { LastNumber = number };

            var batch = container.CreateTransactionalBatch(SharedPartitionKey);
            batch = counterETag.Match(
                Some: etag => batch.ReplaceItem(NumberCounterDocument.CounterId, counter,
                    new TransactionalBatchItemRequestOptions { IfMatchEtag = etag }),
                None: () => batch.CreateItem(counter));
            batch = batch.CreateItem(RiteDocument.From(rite, number));
            batch = fromDraft.Match(
                Some: draft => batch.ReplaceItem(draft.Published.Id, draft.Published,
                    new TransactionalBatchItemRequestOptions { IfMatchEtag = draft.ETag }),
                None: () => batch);
            // Last, so the positions above stay what the checks below read. The rite's publishing is the Machine Spirit's.
            batch = batch.CreateItem(AuditEntryDocument.From(new AuditEntry
            {
                Id = Guid.NewGuid(),
                AtUtc = DateTime.UtcNow,
                Actor = AuditEntry.MachineSpirit,
                Action = AuditAction.Publish,
                DraftId = fromDraft.Map(draft => Guid.Parse(draft.Published.Id)),
                RiteNumber = number,
                Title = rite.Writing.Title,
            }));

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return rite with { Number = number };
            }

            // A batch stops at its first failed operation; the ones after it report 424 (failed dependency).
            var counterStatus = response[0].StatusCode;
            var riteStatus = response[1].StatusCode;
            if (riteStatus == HttpStatusCode.Conflict)
            {
                return DayAlreadyHasRite;
            }

            if (fromDraft.IsSome && response[2].StatusCode == HttpStatusCode.PreconditionFailed)
            {
                return DraftNotWaiting;
            }

            var numberTaken = counterStatus is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
            if (!numberTaken || attempt == MaxSaveAttempts)
            {
                return Error.New(
                    $"Saving the rite for {rite.PublishedOnUtc:yyyy-MM-dd} failed: {response.StatusCode} (counter {counterStatus}, rite {riteStatus}). {response.ErrorMessage}");
            }

            logger.LogInformation("Number {Number} was taken while saving the rite for {Day}; trying the next one.",
                number, rite.PublishedOnUtc);
        }
    }

    private async Task<(int LastNumber, Option<string> ETag)> ReadLastNumber(CancellationToken cancellationToken)
        => (await container.ReadOrNone<NumberCounterDocument>(NumberCounterDocument.CounterId, cancellationToken)).Match(
            Some: found => (found.Document.LastNumber, Some(found.ETag)),
            None: () => (0, Option<string>.None));
}
