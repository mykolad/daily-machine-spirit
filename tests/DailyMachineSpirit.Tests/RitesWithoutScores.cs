using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>The real rites, except that reading scores fails, as when Cosmos falters halfway through a page.</summary>
public sealed class RitesWithoutScores : IRiteRepository
{
    private readonly IRiteRepository rites;

    public RitesWithoutScores(IRiteRepository rites)
    {
        this.rites = rites;
    }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => rites.GetPublishedOn(utcDate, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => rites.GetNewest(count, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => rites.GetByNumbers(numbers, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => rites.GetOlderThan(number, count, cancellationToken);

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => rites.SearchOlderThan(words, tag, number, count, cancellationToken);

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken)
        => rites.FindDaysAndTags(words, tag, cancellationToken);

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken) => rites.GetDays(cancellationToken);

    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken) => rites.GetAllWords(cancellationToken);

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken) => rites.GetAllUnrevealed(cancellationToken);

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken) => rites.GetHereticalTruth(number, cancellationToken);

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => rites.Add(rite, cancellationToken);

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
        => rites.Publish(draftId, publishedOnUtc, cancellationToken);

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => rites.SaveScores(publishedOnUtc, similarity, cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Task.FromResult<Either<Error, Dictionary<int, float[]>>>(Error.New("Cosmos didn't answer."));

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => rites.GetTaggedOlderThan(tag, number, count, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken)
        => rites.GetUntagged(cancellationToken);

    public Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken)
        => rites.SaveTags(publishedOnUtc, tags, cancellationToken);
}
