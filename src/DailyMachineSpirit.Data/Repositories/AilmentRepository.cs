using System.Net;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public sealed class AilmentRepository : IAilmentRepository
{
    private readonly Container container;

    public AilmentRepository(Container container)
    {
        this.container = container;
    }

    // Burned drafts too: a Scribe may restore one. A published draft's words live on in its rite.
    public Task<Either<Error, List<WordsWithoutAilment>>> GetRitesAndUnpublishedDraftsWithoutAilment(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<WordsWithoutAilmentDocument>(
            new QueryDefinition(
                "SELECT c.id, c._etag, c.type, c.kind, c.title, c.text, c.hereticalTruth FROM c " +
                "WHERE ((c.type = @rite) OR (c.type = @draft AND c.state != @published)) AND (NOT IS_DEFINED(c.ailment) OR IS_NULL(c.ailment))")
                .WithParameter("@rite", RiteDocument.RiteType)
                .WithParameter("@draft", DraftDocument.DraftType)
                .WithParameter("@published", JsonNamingPolicy.CamelCase.ConvertName(nameof(DraftState.Published))),
            cancellationToken)).Select(document => document.ToWords()).ToList());

    public Task<Either<Error, Unit>> SaveIfUnchangedSinceRead(WordsWithoutAilment words, string ailment, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            // Older words could hold what an amendment or an erasure took out, and so could an ailment written from them.
            // A batch, since a single patch doesn't honour If-Match: only a batch's patch is refused for another version.
            using var response = await container.CreateTransactionalBatch(SharedPartitionKey)
                .PatchItem(words.DocumentId, [PatchOperation.Set("/ailment", ailment)],
                    new TransactionalBatchPatchItemRequestOptions { IfMatchEtag = words.Version })
                .ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Right<Error, Unit>(unit);
            }
            return response.Any(result => result.StatusCode == HttpStatusCode.PreconditionFailed)
                ? ScriptoriumRepository.ChangedMeanwhile
                : Error.New($"Saving the ailment failed: {response.StatusCode}. {response.ErrorMessage}");
        });
}
