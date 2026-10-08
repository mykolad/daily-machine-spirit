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

    public Task<Either<Error, Option<Rite>>> GetByNumber(int number, CancellationToken cancellationToken)
        => rites.GetByNumber(number, cancellationToken);

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => rites.GetPublishedOn(utcDate, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => rites.GetNewest(count, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => rites.GetByNumbers(numbers, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => rites.GetOlderThan(number, count, cancellationToken);

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => rites.Add(rite, cancellationToken);

    public Task<Either<Error, Option<ReactionCounts>>> React(
        int number, Option<Reaction> reaction, Option<Reaction> previous, CancellationToken cancellationToken)
        => rites.React(number, reaction, previous, cancellationToken);

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => rites.SaveScores(publishedOnUtc, similarity, cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Task.FromResult<Either<Error, Dictionary<int, float[]>>>(Error.New("Cosmos didn't answer."));
}
