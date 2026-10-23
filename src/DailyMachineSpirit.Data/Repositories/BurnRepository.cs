using System.Net;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public class BurnRepository : IBurnRepository
{
    private static readonly string BurnedState = JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Burned));

    // A draft burned before its burn's details were kept has none of them; one burned since has them, null when unknown.
    private static readonly string WithoutDetails = $"c.state = '{BurnedState}' AND NOT IS_DEFINED(c.burnedBy)";

    private readonly Container container;

    public BurnRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, List<Guid>>> GetBurnedWithoutDetails(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<string>(
            new QueryDefinition($"SELECT VALUE c.id FROM c WHERE c.type = @type AND {WithoutDetails}")
                .WithParameter("@type", DraftDocument.DraftType),
            cancellationToken)).Select(Guid.Parse).ToList());

    public Task<Either<Error, RecordedBurn>> FindInTheRecord(Guid draftId, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            var burn =JsonNamingPolicy.CamelCase.ConvertName(nameof(AuditAction.Burn));
            var entry = (await container.Query<AuditEntryDocument>(
                new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.type = @type AND c.draftId = @draftId AND c.action = @action ORDER BY c.atUtc DESC")
                    .WithParameter("@type", AuditEntryDocument.AuditType)
                    .WithParameter("@draftId", draftId)
                    .WithParameter("@action", burn),
                cancellationToken)).HeadOrNone();
            // The newest is picked here, not by the query: a decision's time is written without trailing zeros, so Cosmos
            // would sort a burn at …:00Z after one half a second later. A draft has only a few.
            var decision =(await container.Query<ScribeDecisionDocument>(
                new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.draftId = @draftId AND c.action = @action")
                    .WithParameter("@type", ScribeDecisionDocument.DecisionType)
                    .WithParameter("@draftId", draftId)
                    .WithParameter("@action", JsonNamingPolicy.CamelCase.ConvertName(nameof(ScribeAction.Burn))),
                cancellationToken)).OrderByDescending(found => found.DecidedAtUtc).HeadOrNone();
            return new RecordedBurn(
                entry.Map(found => found.Actor),
                decision.Bind(found => Optional(found.CalendarPlace)),
                decision.Bind(found => Optional(found.Note)));
        });

    // A patch, not a replace, and only while the draft is still as GetBurnedWithoutDetails found it: restored, or burned
    // again with its own details, meanwhile, the Record's older burn mustn't overwrite them.
    public Task<Either<Error, Unit>> SaveDetails(Guid draftId, RecordedBurn burn, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            try
            {
                await container.PatchItemAsync<DraftDocument>(DraftDocument.IdFor(draftId), SharedPartitionKey,
                    [
                        PatchOperation.Set("/burnedBy", burn.By.OrNull()),
                        PatchOperation.Set("/burnedFromPlace", burn.FromPlace.OrNullable()),
                        PatchOperation.Set("/burnReason", burn.Reason.OrNull()),
                    ],
                    new PatchItemRequestOptions { FilterPredicate = $"FROM c WHERE {WithoutDetails}" },
                    cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
            }
            return unit;
        });
}
