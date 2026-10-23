using System.Net;
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

    // Every draft is written by the Machine Spirit (a refill): the record says so, in the same batch.
    public Task<Either<Error, Draft>> Add(Draft draft, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            using var response = await container.CreateTransactionalBatch(SharedPartitionKey)
                .CreateItem(DraftDocument.From(draft))
                .CreateItem(AuditEntryDocument.From(new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    AtUtc = DateTime.UtcNow,
                    Actor = AuditEntry.MachineSpirit,
                    Action = AuditAction.Write,
                    DraftId = draft.Id,
                    Title = draft.Writing.Title,
                }))
                .ExecuteAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? Right<Error, Draft>(draft)
                : Error.New($"Saving the draft failed: {response.StatusCode}. {response.ErrorMessage}");
        });

    public Task<Either<Error, Option<Draft>>> Get(Guid draftId, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken))
            .Map(found => found.Document.ToDraft()));

    public Task<Either<Error, Option<(Draft Draft, string ETag)>>> GetWithETag(Guid draftId, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<DraftDocument>(DraftDocument.IdFor(draftId), cancellationToken))
            .Map(found => (found.Document.ToDraft(), found.ETag)));

    public Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<DraftDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.state = @state")
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@state", JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Waiting))),
            cancellationToken)).Select(document => document.ToDraft()).ToList());

    // A patch, so nothing else on the draft is touched. Changed in any way since the version judged (judged by another,
    // amended, burned, published), the batch fails, and the record gets no entry for a judgment that wasn't saved.
    public Task<Either<Error, Unit>> SaveJudgmentIfUnchanged(Draft judged, string etag, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var document = DraftDocument.From(judged);
            using var response = await container.CreateTransactionalBatch(SharedPartitionKey)
                .PatchItem(document.Id,
                    [
                        PatchOperation.Set("/similarity", document.Similarity),
                        PatchOperation.Set("/augury", document.Augury),
                        PatchOperation.Set("/tags", document.Tags),
                    ],
                    new TransactionalBatchPatchItemRequestOptions { IfMatchEtag = etag })
                .CreateItem(AuditEntryDocument.From(audit))
                .ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Right<Error, Unit>(unit);
            }
            return response.Any(result => result.StatusCode == HttpStatusCode.PreconditionFailed)
                ? ScriptoriumRepository.ChangedMeanwhile
                : Error.New($"Saving the judgment failed: {response.StatusCode}. {response.ErrorMessage}");
        });
}
