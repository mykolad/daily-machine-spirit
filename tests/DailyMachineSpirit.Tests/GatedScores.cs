using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>The real rites, except that reading every rite's scores waits until <see cref="Open"/>, and is counted.</summary>
public sealed class GatedScores : IRiteRepository
{
    private readonly IRiteRepository rites;
    private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int reads;

    public GatedScores(IRiteRepository rites)
    {
        this.rites = rites;
    }

    public int Reads => Volatile.Read(ref reads);

    public void Open() => gate.SetResult();

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => rites.GetPublishedOn(utcDate, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => rites.GetNewest(count, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => rites.GetByNumbers(numbers, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => rites.GetOlderThan(number, count, cancellationToken);

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => rites.Add(rite, cancellationToken);

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
        => rites.Publish(draftId, publishedOnUtc, cancellationToken);

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => rites.SaveScores(publishedOnUtc, similarity, cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => rites.GetScoresByRiteNumber(scoresGeneratorVersion, cancellationToken);

    public async Task<Either<Error, Dictionary<int, RiteSimilarity>>> GetSimilarities(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref reads);
        await gate.Task.WaitAsync(cancellationToken);
        return await rites.GetSimilarities(cancellationToken);
    }
}
