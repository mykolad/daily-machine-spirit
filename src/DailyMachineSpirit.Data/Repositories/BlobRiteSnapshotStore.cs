using System.Net;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites' snapshot as one blob, in a storage account outside the app's region (<c>infra/</c>), reached as the app's
/// managed identity (your <c>az login</c> locally).
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

    public Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            var snapshot = new RiteSnapshotDocument
            {
                WrittenAtUtc = DateTime.UtcNow,
                Rites = rites.Select(rite => RiteDocument.From(rite, rite.Number)).ToList(),
            };
            await blob.UploadAsync(BinaryData.FromObjectAsJson(snapshot, Json), overwrite: true, cancellationToken);
            return unit;
        });

    public Task<Either<Error, Option<List<Rite>>>> Load(CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            try
            {
                var content = await blob.DownloadContentAsync(cancellationToken);
                var snapshot = content.Value.Content.ToObjectFromJson<RiteSnapshotDocument>(Json) ?? new RiteSnapshotDocument();
                return Some(snapshot.Rites.Select(document => document.ToRite()).ToList());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return Option<List<Rite>>.None;
            }
        });
}
