using System.Net;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The Scribes' order of the drafts, as read: <see cref="ETag"/> lets saving it detect another Scribe's change.</summary>
public sealed record Placements(List<Guid> Order, Option<string> ETag);

/// <summary>
/// What the Scribes change: their order of the drafts, and which drafts are burned. Each change is saved in one batch
/// with the <see cref="ScribeDecision"/> that records it, so the lessons for the Augury never miss a change or invent one.
/// Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IScriptoriumRepository
{
    Task<Either<Error, Placements>> GetPlacements(CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when another Scribe saved an order since it was read, or when
    /// any of <paramref name="mustStillWait"/> isn't waiting any more (burned or published meanwhile): a move places
    /// the drafts it passes as well as the one moved, and an order built on a draft that's gone would mislead.
    /// </summary>
    Task<Either<Error, Unit>> SavePlacements(
        Placements placements, Option<ScribeDecision> decision, IReadOnlyCollection<Guid> mustStillWait, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a draft from one state to another (burning, restoring), and saves <paramref name="placements"/> with it when
    /// given (taking the draft out of the Scribes' order). <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when the
    /// draft isn't in <paramref name="from"/> any more, or another Scribe changed the order.
    /// </summary>
    Task<Either<Error, Draft>> ChangeState(
        Guid draftId,
        DraftState from,
        DraftState to,
        ScribeDecision decision,
        Option<Placements> placements,
        CancellationToken cancellationToken);

    /// <summary>Burned drafts, the most recently burned first.</summary>
    Task<Either<Error, List<Draft>>> GetBurned(int count, CancellationToken cancellationToken);

    /// <summary>The newest decisions first.</summary>
    Task<Either<Error, List<ScribeDecision>>> GetDecisions(int count, CancellationToken cancellationToken);
}

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
                Some: found => new Placements(found.Document.Order, found.ETag),
                None: () => new Placements([], None)));

    public Task<Either<Error, Unit>> SavePlacements(
        Placements placements, Option<ScribeDecision> decision, IReadOnlyCollection<Guid> mustStillWait, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var batch = WithPlacements(container.CreateTransactionalBatch(SharedPartitionKey), placements);
            batch = mustStillWait.Distinct().Aggregate(batch, StillWaiting);
            batch = decision.Match(
                Some: made => batch.CreateItem(ScribeDecisionDocument.From(made)),
                None: () => batch);
            return await Execute(batch, cancellationToken);
        });

    public Task<Either<Error, Draft>> ChangeState(
        Guid draftId,
        DraftState from,
        DraftState to,
        ScribeDecision decision,
        Option<Placements> placements,
        CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var stored = await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken);
            return await stored
                .Filter(found => found.Document.State == from)
                .Match(
                    Some: async found =>
                    {
                        var changed = found.Document with
                        {
                            State = to,
                            BurnedAtUtc = to == DraftState.Burned ? Utc.From(decision.DecidedAtUtc) : null,
                        };
                        var batch = container.CreateTransactionalBatch(SharedPartitionKey)
                            .ReplaceItem(changed.Id, changed, new TransactionalBatchItemRequestOptions { IfMatchEtag = found.ETag })
                            .CreateItem(ScribeDecisionDocument.From(decision));
                        batch = placements.Match(Some: order => WithPlacements(batch, order), None: () => batch);
                        return (await Execute(batch, cancellationToken)).Map(_ => changed.ToDraft());
                    },
                    None: () => Task.FromResult(Left<Error, Draft>(ChangedMeanwhile)));
        });

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

    // Fails the batch (412) unless the draft still waits: setting its state to what it already is changes nothing, but
    // only matches a waiting draft. A burned or published draft can't be reordered.
    private static TransactionalBatch StillWaiting(TransactionalBatch batch, Guid draftId)
        => batch.PatchItem(DraftDocument.IdFor(draftId),
            [PatchOperation.Set("/state", WaitingState)],
            new TransactionalBatchPatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.state = '" + WaitingState + "'" });

    // No ETag: nobody has placed a draft yet, and creating the order fails if another Scribe just did.
    private static TransactionalBatch WithPlacements(TransactionalBatch batch, Placements placements)
    {
        var document = new PlacementsDocument { Order = placements.Order };
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
            return unit;
        return response.Any(result => result.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
            ? ChangedMeanwhile
            : Error.New($"Saving the Scribes' change failed: {response.StatusCode}. {response.ErrorMessage}");
    }
}
