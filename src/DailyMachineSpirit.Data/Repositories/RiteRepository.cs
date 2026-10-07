using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
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

    public RiteRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Option<Rite>>> GetByNumber(int number, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", number),
            cancellationToken)).HeadOrNone().Map(document => document.ToRite()));

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<RiteDocument>(RiteDocument.IdFor(utcDate), cancellationToken))
            .Map(found => found.Document.ToRite()));

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.publishedOnUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken)).Select(document => document.ToRite()).ToList());

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

    private sealed record NumberAndScores(int Number, float[] Scores);

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

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                return rite with { Number = number };

            // A batch stops at its first failed operation; the ones after it report 424 (failed dependency).
            var counterStatus = response[0].StatusCode;
            var riteStatus = response[1].StatusCode;
            if (riteStatus == HttpStatusCode.Conflict)
                return DayAlreadyHasRite;
            if (fromDraft.IsSome && response[2].StatusCode == HttpStatusCode.PreconditionFailed)
                return DraftNotWaiting;
            var numberTaken = counterStatus is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
            if (!numberTaken || attempt == MaxSaveAttempts)
                return Error.New(
                    $"Saving the rite for {rite.PublishedOnUtc:yyyy-MM-dd} failed: {response.StatusCode} (counter {counterStatus}, rite {riteStatus}). {response.ErrorMessage}");
        }
    }

    private async Task<(int LastNumber, Option<string> ETag)> ReadLastNumber(CancellationToken cancellationToken)
        => (await container.ReadOrNone<NumberCounterDocument>(NumberCounterDocument.CounterId, cancellationToken)).Match(
            Some: found => (found.Document.LastNumber, Some(found.ETag)),
            None: () => (0, Option<string>.None));
}
