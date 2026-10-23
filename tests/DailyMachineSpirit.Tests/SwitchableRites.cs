using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>The rites from whichever repository is current: Cosmos DB failing, then answering again, within one test.</summary>
public sealed class SwitchableRites : IRiteRepository
{
    public SwitchableRites(IRiteRepository current)
    {
        Current = current;
    }

    public IRiteRepository Current { get; set; }

    /// <summary>A read waits for this before reaching the current repository, as a slow Cosmos DB keeps it waiting.</summary>
    public Option<Task> Hold { get; set; }

    /// <summary>How many reads reached the current repository.</summary>
    public int Reads { get; private set; }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Counted(() => Current.GetPublishedOn(utcDate, cancellationToken));

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Counted(() => Current.GetNewest(count, cancellationToken));

    public Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken)
        => Counted(() => Current.GetAllPublished(cancellationToken));

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => Counted(() => Current.GetByNumbers(numbers, cancellationToken));

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => Counted(() => Current.GetOlderThan(number, count, cancellationToken));

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => Counted(() => Current.SearchOlderThan(words, tag, number, count, cancellationToken));

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken)
        => Counted(() => Current.FindDaysAndTags(words, tag, cancellationToken));

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken) => Counted(() => Current.GetDays(cancellationToken));

    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken) => Counted(() => Current.GetAllWords(cancellationToken));

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken)
        => Counted(() => Current.GetAllUnrevealed(cancellationToken));

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken)
        => Counted(() => Current.GetHereticalTruth(number, cancellationToken));

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => Current.Add(rite, cancellationToken);

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
        => Current.Publish(draftId, publishedOnUtc, cancellationToken);

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => Current.SaveScores(publishedOnUtc, similarity, cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Counted(() => Current.GetScoresByRiteNumber(scoresGeneratorVersion, cancellationToken));

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => Counted(() => Current.GetTaggedOlderThan(tag, number, count, cancellationToken));

    public Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken) => Current.GetUntagged(cancellationToken);

    public Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken)
        => Current.SaveTags(publishedOnUtc, tags, cancellationToken);

    private async Task<T> Counted<T>(Func<Task<T>> read)
    {
        Reads++;
        await Hold.IfNone(Task.CompletedTask);
        return await read();
    }
}
