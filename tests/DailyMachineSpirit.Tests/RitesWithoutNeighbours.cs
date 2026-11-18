using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>The rites as stored, except that the rites before and after one can't be read, as when that read fails.</summary>
public sealed class RitesWithoutNeighbours : IRiteReader
{
    private readonly IRiteReader rites;

    public RitesWithoutNeighbours(IRiteReader rites)
    {
        this.rites = rites;
    }

    public Task<Either<Error, RiteNeighbours>> GetNeighbours(int number, CancellationToken cancellationToken)
        => Task.FromResult<Either<Error, RiteNeighbours>>(Error.New("Cosmos didn't answer."));

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken) => rites.GetPublishedOn(utcDate, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken) => rites.GetNewest(count, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken) => rites.GetAllPublished(cancellationToken);

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken) => rites.GetByNumbers(numbers, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken) => rites.GetOlderThan(number, count, cancellationToken);

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<SoughtWord> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => rites.SearchOlderThan(words, tag, number, count, cancellationToken);

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<SoughtWord> words, Option<string> tag, CancellationToken cancellationToken)
        => rites.FindDaysAndTags(words, tag, cancellationToken);

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken) => rites.GetDays(cancellationToken);

    public Task<Either<Error, List<RiteVotes>>> GetVotes(CancellationToken cancellationToken) => rites.GetVotes(cancellationToken);

    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken) => rites.GetAllWords(cancellationToken);

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken) => rites.GetAllUnrevealed(cancellationToken);

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken) => rites.GetHereticalTruth(number, cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => rites.GetScoresByRiteNumber(scoresGeneratorVersion, cancellationToken);

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => rites.GetTaggedOlderThan(tag, number, count, cancellationToken);
}
