using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Each change is a patch with a condition (Cosmos's filter predicate), so a message delivered twice, a Scribe's
/// amendment or another tab's acceptance meanwhile is found out rather than written over.
/// </summary>
public class FalseTruthsRepository : IFalseTruthsRepository
{
    // A field the documents may lack (older ones), or hold as null (the serializer writes nulls).
    private const string NoFalseTruths = "(NOT IS_DEFINED(c.falseTruths) OR IS_NULL(c.falseTruths))";
    private const string NoProposal = "(NOT IS_DEFINED(c.proposedFalseTruths) OR IS_NULL(c.proposedFalseTruths))";
    private const string AProposal = "(IS_DEFINED(c.proposedFalseTruths) AND NOT IS_NULL(c.proposedFalseTruths))";

    private readonly Container container;

    public FalseTruthsRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, IReadOnlyList<int>>> RitesWithout(CancellationToken cancellationToken)
        => Attempt<IReadOnlyList<int>>(async () => await container.Query<int>(
            new QueryDefinition($"SELECT VALUE c.number FROM c WHERE c.type = @type AND {NoFalseTruths} AND {NoProposal} ORDER BY c.number")
                .WithParameter("@type", RiteDocument.RiteType),
            cancellationToken));

    public Task<Either<Error, Option<Rite>>> RiteWithout(int number, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
                new QueryDefinition($"SELECT * FROM c WHERE c.type = @type AND c.number = @number AND {NoFalseTruths} AND {NoProposal}")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken))
            .HeadOrNone()
            .Map(rite => rite.ToRite()));

    public Task<Either<Error, IReadOnlyList<Guid>>> DraftsWithout(CancellationToken cancellationToken)
        => Attempt<IReadOnlyList<Guid>>(async () => (await container.Query<string>(
                new QueryDefinition($"SELECT VALUE c.id FROM c WHERE c.type = @type AND c.state = @waiting AND {NoFalseTruths}")
                    .WithParameter("@type", DraftDocument.DraftType)
                    .WithParameter("@waiting", "waiting"),
                cancellationToken))
            .Select(Guid.Parse)
            .ToList());

    public Task<Either<Error, Option<bool>>> Propose(int number, FalseTruths proposed, CancellationToken cancellationToken)
        => Attempt(async () => await (await IdOf(number, cancellationToken)).MatchAsync(
            Some: async id => Some(await Patched(
                id, [PatchOperation.Set(RiteDocument.ProposedFalseTruthsField, proposed)], $"FROM c WHERE {NoFalseTruths} AND {NoProposal}", cancellationToken)),
            None: () => Option<bool>.None));

    public Task<Either<Error, bool>> GiveDraft(Guid draftId, FalseTruths falseTruths, CancellationToken cancellationToken)
        => Attempt(() => Patched(
            DraftDocument.IdFor(draftId), [PatchOperation.Set("/falseTruths", falseTruths)],
            $"FROM c WHERE c.type = '{DraftDocument.DraftType}' AND c.state = 'waiting' AND {NoFalseTruths}", cancellationToken));

    public Task<Either<Error, IReadOnlyList<ProposedFalseTruths>>> Proposals(CancellationToken cancellationToken)
        => Attempt<IReadOnlyList<ProposedFalseTruths>>(async () => (await container.Query<RiteDocument>(
                new QueryDefinition($"SELECT * FROM c WHERE c.type = @type AND {AProposal} ORDER BY c.number")
                    .WithParameter("@type", RiteDocument.RiteType),
                cancellationToken))
            .Select(Proposed)
            .ToList());

    public Task<Either<Error, Option<ProposedFalseTruths>>> Proposal(int number, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<RiteDocument>(
                new QueryDefinition($"SELECT * FROM c WHERE c.type = @type AND c.number = @number AND {AProposal}")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken))
            .HeadOrNone()
            .Map(Proposed));

    public Task<Either<Error, bool>> Accept(int number, FalseTruths accepted, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            if ((await IdOf(number, cancellationToken)).Case is not string id)
            {
                return Right<Error, bool>(false);
            }
            var batch = container.CreateTransactionalBatch(SharedPartitionKey)
                .PatchItem(
                    id,
                    [PatchOperation.Set("/falseTruths", accepted), PatchOperation.Set<FalseTruths?>(RiteDocument.ProposedFalseTruthsField, null)],
                    new TransactionalBatchPatchItemRequestOptions { FilterPredicate = $"FROM c WHERE {AProposal}" })
                .CreateItem(AuditEntryDocument.From(audit with { DraftId = await DraftOf(id, cancellationToken) }));
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Right<Error, bool>(true);
            }
            return response.Any(result => result.StatusCode == HttpStatusCode.PreconditionFailed)
                ? Right<Error, bool>(false)
                : Error.New($"Accepting the false truths failed: {response.StatusCode}. {response.ErrorMessage}");
        });

    public Task<Either<Error, bool>> Withdraw(int number, CancellationToken cancellationToken)
        => Attempt(async () => await (await IdOf(number, cancellationToken)).MatchAsync(
            Some: id => Patched(id, [PatchOperation.Set<FalseTruths?>(RiteDocument.ProposedFalseTruthsField, null)], $"FROM c WHERE {AProposal}", cancellationToken),
            None: () => false));

    // A rite's document is named by its day: found by its number.
    private async Task<Option<string>> IdOf(int number, CancellationToken cancellationToken)
        => (await container.Query<string>(
                new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.type = @type AND c.number = @number")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken))
            .HeadOrNone();

    // The rite's id is its day, which the draft it was published from keeps; a rite written on the spot has none.
    private async Task<Option<Guid>> DraftOf(string riteId, CancellationToken cancellationToken)
        => (await container.Query<string>(
                new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.type = @type AND c.publishedOnUtc = @day")
                    .WithParameter("@type", DraftDocument.DraftType)
                    .WithParameter("@day", riteId),
                cancellationToken))
            .HeadOrNone()
            .Map(Guid.Parse);

    private static ProposedFalseTruths Proposed(RiteDocument rite)
        => new(rite.Number, rite.Kind, rite.Title, rite.Text, rite.HereticalTruth, Optional(rite.Ailment), rite.ProposedFalseTruths!);

    // True when patched; false when the condition no longer holds, or the document is gone.
    private async Task<bool> Patched(string id, IReadOnlyList<PatchOperation> operations, string condition, CancellationToken cancellationToken)
    {
        try
        {
            await container.PatchItemAsync<object>(id, SharedPartitionKey, operations,
                new PatchItemRequestOptions { FilterPredicate = condition }, cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed || IsMissingItem(ex))
        {
            return false;
        }
    }
}
