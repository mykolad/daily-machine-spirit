using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public sealed class ScribeNameRepository : IScribeNameRepository
{
    private readonly Container container;

    public ScribeNameRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, StoredScribeNames>> Get(CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<ScribeNamesDocument>(ScribeNamesDocument.ScribeNamesId, cancellationToken))
            .Match(
                Some: found => new StoredScribeNames(found.Document.Names, found.ETag),
                None: () => new StoredScribeNames([], None)));

    // No ETag: no Scribe had a name yet, and creating the document fails if another just did.
    public Task<Either<Error, Unit>> Save(StoredScribeNames read, List<ScribeName> changed, AuditEntry audit, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var document = new ScribeNamesDocument { Names = changed };
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
                : Error.New($"Saving the Scribes' names failed: {response.StatusCode}. {response.ErrorMessage}");
        });
}
