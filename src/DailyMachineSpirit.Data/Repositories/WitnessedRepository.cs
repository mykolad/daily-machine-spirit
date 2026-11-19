using DailyMachineSpirit.Data.Entities;
using System.Net;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// One document per reader, saved under its ETag: the same reader adding from two tabs at once loses neither, as one
/// save fails and is tried again from a fresh read.
/// </summary>
public class WitnessedRepository : IWitnessedRepository
{
    /// <summary>A rite a day for well over twenty years: a cap only an abuser reaches, keeping the document small.</summary>
    public const int MaxRites = 10_000;

    // As for votes: a few tries, each after a short random pause, so requests racing each other don't collide again in step.
    private const int MaxAttempts = 5;
    private static readonly TimeSpan MaxPauseBetweenAttempts = TimeSpan.FromMilliseconds(50);

    private static readonly Error LostRace = Error.New("The witnessed rites changed meanwhile.");

    private readonly Container container;

    public WitnessedRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, IReadOnlyList<int>>> Get(VoterKey voterKey, CancellationToken cancellationToken)
        => Attempt(async () => (await container.ReadOrNone<WitnessedDocument>(WitnessedDocument.IdFor(voterKey), cancellationToken))
            .Match<IReadOnlyList<int>>(Some: found => found.Document.Rites, None: () => []));

    public Task<Either<Error, IReadOnlyList<int>>> Add(VoterKey voterKey, IReadOnlyCollection<int> rites, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                var result = await AddOnce(voterKey, rites, cancellationToken);
                var lostRace = result.Match(Left: error => error == LostRace, Right: _ => false);
                if (!lostRace || attempt == MaxAttempts)
                {
                    return result;
                }
                await Task.Delay(MaxPauseBetweenAttempts * Random.Shared.NextDouble(), cancellationToken);
            }
        });

    public Task<Either<Error, Unit>> Forget(VoterKey voterKey, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            await container.DeleteIfExists<WitnessedDocument>(WitnessedDocument.IdFor(voterKey), cancellationToken);
            return unit;
        });

    private async Task<Either<Error, IReadOnlyList<int>>> AddOnce(VoterKey voterKey, IReadOnlyCollection<int> rites, CancellationToken cancellationToken)
    {
        var id = WitnessedDocument.IdFor(voterKey);
        var existing = await container.ReadOrNone<WitnessedDocument>(id, cancellationToken);
        var kept = existing.Match(Some: found => found.Document.Rites, None: () => []);
        var merged = kept.Union(rites).Order().TakeLast(MaxRites).ToList();
        if (merged.SequenceEqual(kept))
        {
            return merged;
        }
        try
        {
            await existing.Match(
                Some: found => container.ReplaceItemAsync(found.Document with { Rites = merged }, id, SharedPartitionKey,
                    new ItemRequestOptions { IfMatchEtag = found.ETag }, cancellationToken),
                None: () => container.CreateItemAsync(new WitnessedDocument { Id = id, VoterKey = voterKey.Value, Rites = merged },
                    SharedPartitionKey, cancellationToken: cancellationToken));
            return merged;
        }
        // Changed, created or forgotten by another tab since the read.
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict || IsMissingItem(ex))
        {
            return LostRace;
        }
    }
}
