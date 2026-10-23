using System.Net;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites' snapshot as one blob, in a storage account outside the app's region (<c>infra/</c>), reached as the app's
/// managed identity (your <c>az login</c> locally). Its ETag is the copy's version.
/// </summary>
public sealed class BlobRiteSnapshotStore : IRiteSnapshotStore
{
    private const string BlobName = "rites.json";

    private static readonly JsonSerializerOptions Json = CosmosClients.DocumentJson();

    private readonly BlobClient blob;

    public BlobRiteSnapshotStore(Uri containerUri)
    {
        blob = new BlobContainerClient(containerUri, new DefaultAzureCredential()).GetBlobClient(BlobName);
    }

    public Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            try
            {
                var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
                return Some(properties.Value.ETag.ToString());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return Option<string>.None;
            }
        });

    public Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            try
            {
                var content = await blob.DownloadContentAsync(cancellationToken);
                var snapshot = content.Value.Content.ToObjectFromJson<RiteSnapshotDocument>(Json) ?? new RiteSnapshotDocument();
                return snapshot.Discarded
                    ? Option<RiteSnapshot>.None
                    : Some(new RiteSnapshot(snapshot.Rites.Select(document => document.ToRite()).ToList(), content.Value.Details.ETag.ToString()));
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return Option<RiteSnapshot>.None;
            }
        });

    public Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, Option<string> replacing, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            var snapshot = new RiteSnapshotDocument
            {
                WrittenAtUtc = DateTime.UtcNow,
                Rites = rites.Select(rite => RiteDocument.From(rite, rite.Number)).ToList(),
            };
            // Only over the copy read before the rites were: a copy saved meanwhile may hold newer words (an erasure).
            var conditions = replacing.Match(
                Some: version => new BlobRequestConditions { IfMatch = new ETag(version) },
                None: () => new BlobRequestConditions { IfNoneMatch = ETag.All });
            try
            {
                await blob.UploadAsync(BinaryData.FromObjectAsJson(snapshot, Json), new BlobUploadOptions { Conditions = conditions }, cancellationToken);
                return Right<Error, Unit>(unit);
            }
            catch (RequestFailedException ex) when (ex.Status is (int)HttpStatusCode.PreconditionFailed or (int)HttpStatusCode.Conflict)
            {
                return IRiteSnapshotStore.ChangedMeanwhile;
            }
        });

    // A marker, not a deletion: a write that read no copy would otherwise create one with the rites it read, perhaps
    // before the erasure; over the marker, its version check fails and it reads them again.
    public Task<Either<Error, Unit>> Discard(CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            var discarded = new RiteSnapshotDocument { WrittenAtUtc = DateTime.UtcNow, Discarded = true };
            await blob.UploadAsync(BinaryData.FromObjectAsJson(discarded, Json), overwrite: true, cancellationToken);
            return unit;
        });
}
