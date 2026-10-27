using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Reading the published rites, as the public pages, search and the rites API do: from Cosmos DB, or from the rites'
/// snapshot while it's down (<see cref="RitesWithFallback"/>, #112). What reads in order to save takes
/// <see cref="IRiteRepository"/>, which always reads Cosmos DB. Every call returns its failure as an <see cref="Error"/>,
/// never throws one.
/// </summary>
public interface IRiteReader
{
    Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);

    Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken);

    /// <summary>Every published rite, whole, by number: what the snapshot keeps (<see cref="IRiteSnapshotStore"/>).</summary>
    Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken);

    /// <summary>The rites with these numbers, in no particular order; numbers without a rite are left out.</summary>
    Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken);

    /// <summary>At most <paramref name="count"/> rites, the newest first.</summary>
    Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken);

    /// <summary>
    /// At most <paramref name="count"/> rites holding every one of <paramref name="words"/>, each in their title, text or
    /// Heretical Truth, ignoring case, or by a subject it names, and tagged <paramref name="tag"/> if one is given, the
    /// newest first.
    /// </summary>
    Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<SoughtWord> words, Option<string> tag, int number, int count, CancellationToken cancellationToken);

    /// <summary>The number, day and tags of every rite <see cref="SearchOlderThan"/> finds, however many, the newest first.</summary>
    Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<SoughtWord> words, Option<string> tag, CancellationToken cancellationToken);

    /// <summary>Every rite's number and day, the newest first.</summary>
    Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken);

    /// <summary>Every rite's words, the newest first, without scores, votes or tags.</summary>
    Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken);

    /// <summary>Every rite before its Heretical Truth is revealed, the newest first.</summary>
    Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken);

    /// <summary>The Heretical Truth of the rite with that number; None when no rite has it.</summary>
    Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken);

    Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);

    /// <summary>At most <paramref name="count"/> rites with this tag, the newest first.</summary>
    Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken);
}
