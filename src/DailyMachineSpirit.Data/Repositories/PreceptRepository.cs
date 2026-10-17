using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public sealed class PreceptRepository : IPreceptRepository
{
    private readonly Container container;

    public PreceptRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, StoredPrecepts>> Get(CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<PreceptsDocument>(PreceptsDocument.PreceptsId, cancellationToken))
            .Match(
                Some: found => new StoredPrecepts(found.Document.ToBook(), found.ETag),
                None: () => new StoredPrecepts(new PreceptBook(), None)));

    // No ETag: there were no Precepts yet, and creating them fails if someone else just did.
    public Task<Either<Error, Unit>> Save(StoredPrecepts read, PreceptBook changed, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var document = PreceptsDocument.From(changed);
            var batch = container.CreateTransactionalBatch(SharedPartitionKey);
            batch = read.ETag.Match(
                Some: etag => batch.ReplaceItem(document.Id, document, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag }),
                None: () => batch.CreateItem(document));
            using var response = await batch.CreateItem(AuditEntryDocument.From(audit)).ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Right<Error, Unit>(unit);
            }

            return response.Any(result => result.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
                ? ScriptoriumRepository.ChangedMeanwhile
                : Error.New($"Saving the Precepts failed: {response.StatusCode}. {response.ErrorMessage}");
        });
}
