using System.Net;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public class ScriptoriumRepository : IScriptoriumRepository
{
    public static readonly Error ChangedMeanwhile = Error.New("Another Scribe changed this meanwhile.");

    private static readonly string WaitingState = JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Waiting));

    private readonly Container container;

    public ScriptoriumRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Placements>> GetPlacements(CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<PlacementsDocument>(PlacementsDocument.PlacementsId, cancellationToken))
            .Match(
                Some: found => new Placements(found.Document.Order, found.Document.PlacedBy, found.ETag),
                None: () => new Placements([], new Dictionary<Guid, string>(), None)));

    public Task<Either<Error, Unit>> SavePlacements(
        Placements placements, Option<ScribeDecision> decision, IReadOnlyCollection<Guid> mustStillWait, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var batch = WithPlacements(container.CreateTransactionalBatch(SharedPartitionKey), placements);
            batch = mustStillWait.Distinct().Aggregate(batch, StillWaiting);
            batch = decision.Match(
                Some: made => batch.CreateItem(ScribeDecisionDocument.From(made)),
                None: () => batch);
            batch = batch.CreateItem(AuditEntryDocument.From(audit));
            return await Execute(batch, cancellationToken);
        });

    public Task<Either<Error, Draft>> ChangeState(
        Guid draftId,
        DraftState from,
        DraftState to,
        ScribeDecision decision,
        Option<Placements> placements,
        AuditEntry audit,
        CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var stored = await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken);
            return await stored
                .Filter(found => found.Document.State == from)
                .Match(
                    Some: async found =>
                    {
                        // The Ashes say who burned it, from where and why; a restored draft forgets its burn, which
                        // the Record and the decisions keep.
                        var burned = to == DraftState.Burned;
                        var changed = found.Document with
                        {
                            State = to,
                            BurnedAtUtc = burned ? Utc.From(decision.DecidedAtUtc) : null,
                            BurnedBy = burned ? audit.Actor : null,
                            BurnedFromPlace = burned ? decision.CalendarPlace.OrNullable() : null,
                            BurnReason = burned ? decision.Note.OrNull() : null,
                        };
                        var batch = container.CreateTransactionalBatch(SharedPartitionKey)
                            .ReplaceItem(changed.Id, changed, new TransactionalBatchItemRequestOptions { IfMatchEtag = found.ETag })
                            .CreateItem(ScribeDecisionDocument.From(decision))
                            .CreateItem(AuditEntryDocument.From(audit));
                        batch = placements.Match(Some: order => WithPlacements(batch, order), None: () => batch);
                        return (await Execute(batch, cancellationToken)).Map(_ => changed.ToDraft());
                    },
                    None: () => Task.FromResult(Left<Error, Draft>(ChangedMeanwhile)));
        });

    public Task<Either<Error, Unit>> Amend(Draft amended, string etag, ScribeDecision decision, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var document = DraftDocument.From(amended);
            var batch = container.CreateTransactionalBatch(SharedPartitionKey)
                .ReplaceItem(document.Id, document, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag })
                .CreateItem(ScribeDecisionDocument.From(decision))
                .CreateItem(AuditEntryDocument.From(audit));
            return await Execute(batch, cancellationToken);
        });

    public Task<Either<Error, int>> CountBurned(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.type = @type AND c.state = @state")
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@state", JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Burned))),
            cancellationToken)).Single());

    public Task<Either<Error, List<Draft>>> GetBurned(int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<DraftDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND c.state = @state ORDER BY c.burnedAtUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@state", JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Burned))),
            cancellationToken)).Select(document => document.ToDraft()).ToList());

    public Task<Either<Error, List<ScribeDecision>>> GetDecisions(int count, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<ScribeDecisionDocument>(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.decidedAtUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@type", ScribeDecisionDocument.DecisionType),
            cancellationToken)).Select(document => document.ToDecision()).ToList());

    // Compared as strings, which is how the times are stored: any time on the day ("2026-10-01T00:00:00Z") sorts after
    // the day alone, however many decimals it was written with.
    public Task<Either<Error, List<ScribeDecision>>> GetDecisionsSince(DateOnly day, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<ScribeDecisionDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.decidedAtUtc >= @day")
                .WithParameter("@type", ScribeDecisionDocument.DecisionType)
                .WithParameter("@day", day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            cancellationToken)).Select(document => document.ToDecision()).ToList());

    // Fails the batch (412) unless the draft still waits: setting its state to what it already is changes nothing, but
    // only matches a waiting draft. A burned or published draft can't be reordered.
    private static TransactionalBatch StillWaiting(TransactionalBatch batch, Guid draftId)
        => batch.PatchItem(DraftDocument.IdFor(draftId),
            [PatchOperation.Set("/state", WaitingState)],
            new TransactionalBatchPatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.state = '" + WaitingState + "'" });

    // No ETag: nobody has placed a draft yet, and creating the order fails if another Scribe just did. Who placed a
    // draft is kept only while it's in the order.
    private static TransactionalBatch WithPlacements(TransactionalBatch batch, Placements placements)
    {
        var document = new PlacementsDocument
        {
            Order = placements.Order,
            PlacedBy = placements.PlacedBy.Where(placed => placements.Order.Contains(placed.Key)).ToDictionary(),
        };
        return placements.ETag.Match(
            Some: etag => batch.ReplaceItem(PlacementsDocument.PlacementsId, document,
                new TransactionalBatchItemRequestOptions { IfMatchEtag = etag }),
            None: () => batch.CreateItem(document));
    }

    // A failed ETag or create means another Scribe got there first; anything else is an outage.
    private static async Task<Either<Error, Unit>> Execute(TransactionalBatch batch, CancellationToken cancellationToken)
    {
        using var response = await batch.ExecuteAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return unit;
        }

        return response.Any(result => result.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
            ? ChangedMeanwhile
            : Error.New($"Saving the Scribes' change failed: {response.StatusCode}. {response.ErrorMessage}");
    }
}
