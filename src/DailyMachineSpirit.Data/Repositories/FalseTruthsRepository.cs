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
                id, [PatchOperation.Set("/proposedFalseTruths", proposed)], $"FROM c WHERE {NoFalseTruths} AND {NoProposal}", cancellationToken)),
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
            .Select(rite => new ProposedFalseTruths(rite.Number, rite.Kind, rite.Title, rite.Text, rite.HereticalTruth, rite.ProposedFalseTruths!))
            .ToList());

    public Task<Either<Error, bool>> Accept(int number, FalseTruths accepted, CancellationToken cancellationToken)
        => Attempt(async () => await (await IdOf(number, cancellationToken)).MatchAsync(
            Some: id => Patched(
                id, [PatchOperation.Set("/falseTruths", accepted), PatchOperation.Set<FalseTruths?>("/proposedFalseTruths", null)],
                $"FROM c WHERE {AProposal}", cancellationToken),
            None: () => false));

    public Task<Either<Error, bool>> Withdraw(int number, CancellationToken cancellationToken)
        => Attempt(async () => await (await IdOf(number, cancellationToken)).MatchAsync(
            Some: id => Patched(id, [PatchOperation.Set<FalseTruths?>("/proposedFalseTruths", null)], $"FROM c WHERE {AProposal}", cancellationToken),
            None: () => false));

    // A rite's document is named by its day: found by its number.
    private async Task<Option<string>> IdOf(int number, CancellationToken cancellationToken)
        => (await container.Query<string>(
                new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.type = @type AND c.number = @number")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken))
            .HeadOrNone();

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
