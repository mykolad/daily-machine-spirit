using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites' snapshot, read in memory as <see cref="RiteRepository"/> reads Cosmos DB, query by query, so the pages
/// look the same from either (#112). Read-only: nothing can be saved to a copy.
/// </summary>
public sealed class SnapshotRites : IRiteRepository
{
    public static readonly Error ReadOnly = Error.New("The rites are read from their snapshot just now: nothing can be saved.");

    // Newest first, by number, as the queries return them.
    private readonly List<Rite> rites;

    public SnapshotRites(IEnumerable<Rite> rites)
    {
        this.rites = rites.OrderByDescending(rite => rite.Number).ToList();
    }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Answer(rites.Where(rite => rite.PublishedOnUtc == utcDate).HeadOrNone());

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Answer(rites.OrderByDescending(rite => rite.PublishedOnUtc).Take(count).ToList());

    public Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken)
        => Answer(rites.ToList());

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => Answer(rites.Where(rite => numbers.Contains(rite.Number)).ToList());

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => Answer(rites.Where(rite => rite.Number < number).Take(count).ToList());

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => Answer(Holding(words, tag).Where(rite => rite.Number < number).Take(count).ToList());

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken)
        => Answer(Holding(words, tag).Select(rite => new FoundRite(rite.Number, rite.PublishedOnUtc, rite.Tags.IfNone([]))).ToList());

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken)
        => Answer(rites.Select(rite => new RiteDay(rite.Number, rite.PublishedOnUtc)).ToList());

    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken)
        => Answer(rites.Select(rite => new RiteWords(
            rite.Number, rite.PublishedOnUtc, rite.Writing.Kind, rite.Writing.Title, rite.Writing.Text, rite.Writing.HereticalTruth)).ToList());

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken)
        => Answer(rites.Select(rite => new UnrevealedRite(
            rite.Number, rite.PublishedOnUtc, rite.Writing.Kind, rite.Writing.Title, rite.Writing.Text, rite.Tags.IfNone([]))).ToList());

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken)
        => Answer(rites.Where(rite => rite.Number == number).Select(rite => rite.Writing.HereticalTruth).HeadOrNone());

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Answer(rites
            .SelectMany(rite => rite.Similarity
                .Filter(similarity => similarity.ScoresGeneratorVersion == scoresGeneratorVersion)
                .Map(similarity => (rite.Number, similarity.Scores)))
            .ToDictionary(scored => scored.Number, scored => scored.Scores));

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => Answer(rites.Where(rite => FiledUnder(rite, tag) && rite.Number < number).Take(count).ToList());

    // What tagging the older rites reads, to save their tags: nothing can be saved here.
    public Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken) => Refuse<List<Rite>>();

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => Refuse<Rite>();

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken) => Refuse<Rite>();

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken) => Refuse<Unit>();

    public Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken) => Refuse<Unit>();

    private static Task<Either<Error, T>> Answer<T>(T answer) => Task.FromResult(Right<Error, T>(answer));

    private static Task<Either<Error, T>> Refuse<T>() => Task.FromResult(Left<Error, T>(ReadOnly));

    // As RiteRepository's CONTAINS: every word somewhere in the title, text or Heretical Truth, ignoring case.
    private IEnumerable<Rite> Holding(IReadOnlyList<string> words, Option<string> tag)
        => rites.Where(rite =>
            words.All(word => new[] { rite.Writing.Title, rite.Writing.Text, rite.Writing.HereticalTruth }
                .Any(part => part.Contains(word, StringComparison.OrdinalIgnoreCase)))
            && tag.ForAll(key => FiledUnder(rite, key)));

    private static bool FiledUnder(Rite rite, string tag) => rite.Tags.Exists(tags => tags.Contains(tag));
}
