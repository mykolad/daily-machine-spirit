using System.Net;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Data.Repositories.CosmosCalls;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

public sealed class ExpurgationRepository : IExpurgationRepository
{
    /// <summary>A transactional batch takes at most 100 operations: the copies, and the erasure's entry.</summary>
    public const int MaxCopies = 99;

    public static readonly Error TooManyCopies = Error.New($"The rite's words are kept in more than {MaxCopies} places, too many to erase at once.");

    private readonly Container container;

    public ExpurgationRepository(Container container)
    {
        this.container = container;
    }

    public Task<Either<Error, Option<KeptWords>>> FindWordsOf(int number, CancellationToken cancellationToken)
        => Attempt(async () =>
        {
            var rites = await Words(
                new QueryDefinition("SELECT c.id, c._etag, c.title, c.text, c.hereticalTruth, c.ailment FROM c WHERE c.type = @type AND c.number = @number")
                    .WithParameter("@type", RiteDocument.RiteType)
                    .WithParameter("@number", number),
                cancellationToken);
            return await rites.HeadOrNone().MatchAsync(
                Some: async rite => Some(await CopiesOf(rite, number, cancellationToken)),
                None: () => Option<KeptWords>.None);
        });

    public Task<Either<Error, Unit>> Erase(IReadOnlyList<WordsKept> changed, AuditEntry audit, CancellationToken cancellationToken)
    {
        if (changed.Count > MaxCopies)
        {
            return Task.FromResult(Left<Error, Unit>(TooManyCopies));
        }
        return AttemptEither(async () =>
        {
            var batch = changed.Aggregate(container.CreateTransactionalBatch(SharedPartitionKey), (patched, copy) => patched.PatchItem(
                copy.DocumentId,
                copy.Fields.Select(field => PatchOperation.Set(field.Key, field.Value)).ToList(),
                new TransactionalBatchPatchItemRequestOptions { IfMatchEtag = copy.ETag }));
            using var response = await batch.CreateItem(AuditEntryDocument.From(audit)).ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Right<Error, Unit>(unit);
            }
            return response.Any(result => result.StatusCode == HttpStatusCode.PreconditionFailed)
                ? ScriptoriumRepository.ChangedMeanwhile
                : Error.New($"Erasing the words failed: {response.StatusCode}. {response.ErrorMessage}");
        });
    }

    // The rite's id is its day, which the draft it was published from keeps; the draft's id leads to the decisions and
    // the Record's entries about it. A rite written on the spot, with no draft, has only its own entries.
    private async Task<KeptWords> CopiesOf(WordsDocument rite, int number, CancellationToken cancellationToken)
    {
        var drafts = await Words(
            new QueryDefinition("SELECT c.id, c._etag, c.title, c.text, c.hereticalTruth, c.ailment FROM c WHERE c.type = @type AND c.publishedOnUtc = @day")
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@day", rite.Id),
            cancellationToken);
        var draftIds = drafts.Select(draft => draft.Id).ToList();
        var decisions = await Words(
            new QueryDefinition("SELECT c.id, c._etag, c.title, c.text, c.hereticalTruth, c.ailment, c.note FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@drafts, c.draftId)")
                .WithParameter("@type", ScribeDecisionDocument.DecisionType)
                .WithParameter("@drafts", draftIds),
            cancellationToken);
        var entries = await Words(
            new QueryDefinition("SELECT c.id, c._etag, c.title, c.detail FROM c WHERE c.type = @type AND (c.riteNumber = @number OR ARRAY_CONTAINS(@drafts, c.draftId))")
                .WithParameter("@type", AuditEntryDocument.AuditType)
                .WithParameter("@number", number)
                .WithParameter("@drafts", draftIds),
            cancellationToken);
        // Another draft marked as much like this rite names it by its title then: before an amendment, any it had.
        var titles = new[] { rite }.Concat(drafts).Concat(decisions).Concat(entries)
            .Select(copy => copy.Title).OfType<string>().Distinct().ToList();
        var resembling = await Words(
            new QueryDefinition("SELECT c.id, c._etag, c.resembles FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@titles, c.resembles)")
                .WithParameter("@type", DraftDocument.DraftType)
                .WithParameter("@titles", titles),
            cancellationToken);
        List<WordsKept> copies =
        [
            rite.ToWordsKept(),
            .. drafts.Concat(decisions).Concat(entries).Concat(resembling.Where(draft => !draftIds.Contains(draft.Id))).Select(found => found.ToWordsKept()),
        ];
        return new KeptWords(draftIds.HeadOrNone().Map(Guid.Parse), copies);
    }

    private Task<List<WordsDocument>> Words(QueryDefinition query, CancellationToken cancellationToken)
        => container.Query<WordsDocument>(query, cancellationToken);
}
