using System.Globalization;
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

    public Task<Either<Error, List<AuditEntry>>> GetNewest(int count, CancellationToken cancellationToken)
        => Entries(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type ORDER BY c.atUtc DESC").WithParameter("@count", count),
            cancellationToken);

    // Cosmos DB compares the stored times as text, so the bound is written exactly as they are (SortableUtcConverter's
    // seven decimals). Written shorter, "…T00:00:00Z" would sort after "…T00:00:00.0000000Z" and let midnight's entries in.
    public Task<Either<Error, List<AuditEntry>>> GetNewestBefore(DateTime beforeUtc, int count, CancellationToken cancellationToken)
        => Entries(
            new QueryDefinition("SELECT TOP @count * FROM c WHERE c.type = @type AND c.atUtc < @before ORDER BY c.atUtc DESC")
                .WithParameter("@count", count)
                .WithParameter("@before", Utc.From(beforeUtc).ToString(SortableUtcConverter.Format, CultureInfo.InvariantCulture)),
            cancellationToken);

    public Task<Either<Error, int>> Count(CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.type = @type").WithParameter("@type", AuditEntryDocument.AuditType),
            cancellationToken)).Single());

    public Task<Either<Error, List<AuditEntry>>> GetHistory(Guid draftId, CancellationToken cancellationToken)
        => Entries(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.draftId = @draftId ORDER BY c.atUtc DESC")
                .WithParameter("@draftId", draftId),
            cancellationToken);

    private Task<Either<Error, List<AuditEntry>>> Entries(QueryDefinition query, CancellationToken cancellationToken)
        => Attempt(async () => (await container.Query<AuditEntryDocument>(
            query.WithParameter("@type", AuditEntryDocument.AuditType), cancellationToken))
            .Select(document => document.ToEntry()).ToList());
}
