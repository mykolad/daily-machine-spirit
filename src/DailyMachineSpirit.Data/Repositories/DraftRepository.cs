using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Every call returns its failure as an <see cref="Error"/>, never throws one. Publishing a draft is <see cref="IRiteRepository.Publish"/>.</summary>
public interface IDraftRepository
{
    Task<Either<Error, Draft>> Add(Draft draft, CancellationToken cancellationToken);

    /// <summary>The draft, whatever its state; None when there's no such draft.</summary>
    Task<Either<Error, Option<Draft>>> Get(Guid draftId, CancellationToken cancellationToken);

    /// <summary>The drafts that can still be published, in no particular order (the Augury orders them).</summary>
    Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken);
}

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
}
