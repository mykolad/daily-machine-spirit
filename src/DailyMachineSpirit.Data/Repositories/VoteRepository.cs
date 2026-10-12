using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// A vote and the rite's counts change together, in one transactional batch: the counts are always the votes there are,
/// however many readers vote at once.
/// </summary>
public class VoteRepository : IVoteRepository
{
    // The same reader voting from two tabs at once: one batch fails on the vote's ETag (or finds it created or deleted
    // meanwhile), and is tried again from a fresh read.
    private const int MaxAttempts = 3;

    private static readonly Error LostRace = Error.New("The vote changed meanwhile.");

    private readonly Container container;

    public VoteRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Option<ReactionCounts>>> Vote(
        int riteNumber, string voterKey, Option<Reaction> vote, CancellationToken cancellationToken)
        => AttemptEither(async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                var result = await VoteOnce(riteNumber, voterKey, vote, cancellationToken);
                var lostRace = result.Match(Left: error => error == LostRace, Right: _ => false);
                if (!lostRace || attempt == MaxAttempts)
                {
                    return result;
                }
            }
        });

    public Task<Either<Error, Dictionary<int, Reaction>>> GetVotes(
        string voterKey, IReadOnlyCollection<int> riteNumbers, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            if (riteNumbers.Count == 0)
            {
                return new Dictionary<int, Reaction>();
            }
            var ids = riteNumbers.Distinct().Select(number => (VoteDocument.IdFor(number, voterKey), SharedPartitionKey)).ToList();
            var found = await container.ReadManyItemsAsync<VoteDocument>(ids, cancellationToken: cancellationToken);
            return found.ToDictionary(document => document.RiteNumber, document => document.Reaction);
        });

    private async Task<Either<Error, Option<ReactionCounts>>> VoteOnce(
        int riteNumber, string voterKey, Option<Reaction> vote, CancellationToken cancellationToken)
    {
        var rite = (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", riteNumber),
            cancellationToken)).HeadOrNone();
        return await rite.MatchAsync(
            Some: found => Move(found, voterKey, vote, cancellationToken),
            None: () => Right<Error, Option<ReactionCounts>>(None));
    }

    private async Task<Either<Error, Option<ReactionCounts>>> Move(
        RiteDocument rite, string voterKey, Option<Reaction> vote, CancellationToken cancellationToken)
    {
        var id = VoteDocument.IdFor(rite.Number, voterKey);
        var existing = await container.ReadOrNone<VoteDocument>(id, cancellationToken);
        var previous = existing.Map(found => found.Document.Reaction);
        if (previous == vote)
        {
            return Some(Counts(rite));
        }

        var batch = container.CreateTransactionalBatch(SharedPartitionKey);
        batch = existing.Match(
            Some: found => vote.Match(
                Some: reaction => batch.ReplaceItem(id, found.Document with { Reaction = reaction }, IfMatch(found.ETag)),
                None: () => batch.DeleteItem(id, IfMatch(found.ETag))),
            None: () => vote.Match(
                Some: reaction => batch.CreateItem(new VoteDocument { Id = id, RiteNumber = rite.Number, VoterKey = voterKey, Reaction = reaction }),
                None: () => batch));
        List<PatchOperation> counts =
        [
            .. previous.Map(reaction => PatchOperation.Increment($"/{CountField(reaction)}", -1)),
            .. vote.Map(reaction => PatchOperation.Increment($"/{CountField(reaction)}", 1)),
        ];
        batch = batch.PatchItem(rite.Id, counts);

        using var response = await batch.ExecuteAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return Some(Counts(response.GetOperationResultAtIndex<RiteDocument>(1).Resource));
        }
        return response[0].StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict or HttpStatusCode.NotFound
            ? LostRace
            : Error.New($"Saving a vote on rite NO. {rite.Number} failed: {response.StatusCode}. {response.ErrorMessage}");
    }

    private static TransactionalBatchItemRequestOptions IfMatch(string etag) => new() { IfMatchEtag = etag };

    private static ReactionCounts Counts(RiteDocument rite) => new(rite.BlessedVotes, rite.HeresyVotes);

    private static string CountField(Reaction reaction) => reaction == Reaction.Blessed ? "blessedVotes" : "heresyVotes";
}
