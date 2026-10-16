using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly Container container;

    public AuditRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Unit>> Add(AuditEntry entry, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.CreateItemAsync(AuditEntryDocument.From(entry), SharedPartitionKey, cancellationToken: cancellationToken);
            return unit;
        });

    public Task<Either<Error, List<AuditEntry>>> GetNewest(int count, Option<Guid> draftId, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<AuditEntryDocument>(
            draftId.Match(
                Some: id => new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND c.draftId = @draftId ORDER BY c.atUtc DESC")
                    .WithParameter("@draftId", id),
                None: () => new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.atUtc DESC"))
                .WithParameter("@count", count)
                .WithParameter("@type", AuditEntryDocument.AuditType),
            cancellationToken)).Select(document => document.ToEntry()).ToList());
}
