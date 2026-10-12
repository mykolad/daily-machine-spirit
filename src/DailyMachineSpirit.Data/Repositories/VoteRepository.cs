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
    // meanwhile), and is tried again from a fresh read, after a short random pause, so requests racing each other don't
    // collide again in step.
    private const int MaxAttempts = 5;
    private static readonly TimeSpan MaxPauseBetweenAttempts = TimeSpan.FromMilliseconds(50);

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
                await Task.Delay(MaxPauseBetweenAttempts * Random.Shared.NextDouble(), cancellationToken);
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

    // The vote is read before the rite: a vote saved by the reader's other tab before this read is then already in the
    // counts read after it, so an unchanged vote never comes back with counts that leave it out.
    private async Task<Either<Error, Option<ReactionCounts>>> VoteOnce(
        int riteNumber, string voterKey, Option<Reaction> vote, CancellationToken cancellationToken)
    {
        var id = VoteDocument.IdFor(riteNumber, voterKey);
        var existing = await container.ReadOrNone<VoteDocument>(id, cancellationToken);
        var rite = (await container.Query<RiteDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.number = @number")
                .WithParameter("@type", RiteDocument.RiteType)
                .WithParameter("@number", riteNumber),
            cancellationToken)).HeadOrNone();
        return await rite.MatchAsync(
            Some: found => Move(found, id, existing, voterKey, vote, cancellationToken),
            None: () => Right<Error, Option<ReactionCounts>>(None));
    }

    // Every answer comes from one transactional batch: the vote, changed only if it's still as read (a change by the
    // reader's other tab fails the batch, which is tried again), then the rite, with its counts moved or as they are. So
    // the counts answered always belong with the vote that was asked for.
    private async Task<Either<Error, Option<ReactionCounts>>> Move(
        RiteDocument rite, string id, Option<(VoteDocument Document, string ETag)> existing, string voterKey, Option<Reaction> vote,
        CancellationToken cancellationToken)
    {
        var previous = existing.Map(found => found.Document.Reaction);
        var batch = container.CreateTransactionalBatch(SharedPartitionKey);
        batch = existing.Match(
            Some: found => vote.Match(
                // Unchanged too: saving the same vote again still checks its ETag.
                Some: reaction => batch.ReplaceItem(id, found.Document with { Reaction = reaction }, IfMatch(found.ETag)),
                None: () => batch.DeleteItem(id, IfMatch(found.ETag))),
            None: () => vote.Match(
                Some: reaction => batch.CreateItem(NewVote(id, rite.Number, voterKey, reaction)),
                // No vote, and none asked for: creating one and deleting it again changes nothing, but fails if the
                // reader's other tab saved a vote meanwhile.
                None: () => batch.CreateItem(NewVote(id, rite.Number, voterKey, Reaction.Blessed)).DeleteItem(id)));
        var riteIndex = existing.IsNone && vote.IsNone ? 2 : 1;
        List<PatchOperation> counts =
        [
            .. previous.Map(reaction => PatchOperation.Increment($"/{CountField(reaction)}", -1)),
            .. vote.Map(reaction => PatchOperation.Increment($"/{CountField(reaction)}", 1)),
        ];
        batch = previous == vote ? batch.ReadItem(rite.Id) : batch.PatchItem(rite.Id, counts);

        using var response = await batch.ExecuteAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return Some(Counts(response.GetOperationResultAtIndex<RiteDocument>(riteIndex).Resource));
        }
        return response[0].StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict or HttpStatusCode.NotFound
            ? LostRace
            : Error.New($"Saving a vote on rite NO. {rite.Number} failed: {response.StatusCode}. {response.ErrorMessage}");
    }

    private static VoteDocument NewVote(string id, int riteNumber, string voterKey, Reaction reaction)
        => new() { Id = id, RiteNumber = riteNumber, VoterKey = voterKey, Reaction = reaction };

    private static TransactionalBatchItemRequestOptions IfMatch(string etag) => new() { IfMatchEtag = etag };

    private static ReactionCounts Counts(RiteDocument rite) => new(rite.BlessedVotes, rite.HeresyVotes);

    private static string CountField(Reaction reaction) => reaction == Reaction.Blessed ? "blessedVotes" : "heresyVotes";
}
