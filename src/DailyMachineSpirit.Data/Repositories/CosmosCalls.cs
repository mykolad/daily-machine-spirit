using System.Net;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>What every repository does around its Cosmos calls.</summary>
internal static class CosmosCalls
{
    /// <summary>
    /// Every document shares one partition. A few hundred small rites and drafts a year are far below a partition's
    /// limits, and one partition lets a transactional batch publish a draft, save the rite and take its number together.
    /// </summary>
    public const string SharedPartition = "rites";

    public static readonly PartitionKey SharedPartitionKey = new(SharedPartition);

    // Any Cosmos call can fail (network, throttling, a missing role, an unreadable document): callers get that as an Error
    // to handle, with the exception inside for the logs. Cancellation still throws, as everywhere in .NET.
    public static Task<Either<Error, T>> Attempt<T>(Func<Task<T>> operation)
        => AttemptEither(async () => Right<Error, T>(await operation()));

    public static async Task<Either<Error, T>> AttemptEither<T>(Func<Task<Either<Error, T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.New(ex);
        }
    }

    // A missing document is 404 with sub-status 0. A missing container or database is also 404 (sub-status 1003), but
    // that's an outage, which must reach the caller as an error rather than as "nothing there".
    private static bool IsMissingItem(CosmosException ex) => ex is { StatusCode: HttpStatusCode.NotFound, SubStatusCode: 0 };

    /// <summary>The document and its ETag, or None when there's no such document.</summary>
    public static async Task<Option<(T Document, string ETag)>> ReadOrNone<T>(
        this Container container, string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<T>(id, SharedPartitionKey, cancellationToken: cancellationToken);
            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (IsMissingItem(ex))
        {
            return None;
        }
    }

    public static async Task<List<T>> Query<T>(this Container container, QueryDefinition query, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions { PartitionKey = SharedPartitionKey });
        while (iterator.HasMoreResults)
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        return results;
    }
}
