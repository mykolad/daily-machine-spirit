using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public class DraftRepository : IDraftRepository
{
    private readonly Container container;

    public DraftRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Draft>> Add(Draft draft, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.CreateItemAsync(DraftDocument.From(draft), SharedPartitionKey, cancellationToken: cancellationToken);
            return draft;
        });

    public Task<Either<Error, Option<Draft>>> Get(Guid draftId, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken))
            .Map(found => found.Document.ToDraft()));

    public Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<DraftDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.state = @state")
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@state", JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Waiting))),
            cancellationToken)).Select(document => document.ToDraft()).ToList());

    public Task<Either<Error, List<Draft>>> GetUntagged(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<DraftDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND IS_OBJECT(c.similarity) AND NOT IS_ARRAY(c.tags)")
                .WithParameter("@type", DraftDocument.DraftType),
            cancellationToken)).Select(document => document.ToDraft()).ToList());

    // A patch, not a replace: a Scribe's change made meanwhile stays. It changes the draft's ETag, so a Scribe acting on
    // the page from before is told to look again, as after any change.
    public Task<Either<Error, Unit>> SaveTags(Guid draftId, string[] tags, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.PatchItemAsync<DraftDocument>(DraftDocument.IdFor(draftId), SharedPartitionKey,
                [PatchOperation.Set("/tags", tags)], cancellationToken: cancellationToken);
            return unit;
        });
}
