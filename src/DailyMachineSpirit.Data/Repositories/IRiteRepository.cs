using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Every call returns its failure as an <see cref="Error"/> (Cosmos down, throttled, unreadable data), never throws one.</summary>
public interface IRiteRepository
{
    Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken);

    Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken);

    /// <summary>The rites with these numbers, in no particular order; numbers without a rite are left out.</summary>
    Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken);

    /// <summary>A page of the archive, the newest first.</summary>
    Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken);

    /// <summary>
    /// A page of search results, the newest first: the rites whose title, text or Heretical Truth holds every one of
    /// <paramref name="words"/>, ignoring case, and filed under <paramref name="tag"/> if one is given.
    /// </summary>
    Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken);

    /// <summary>The number, day and tags of every rite <see cref="SearchOlderThan"/> finds, on every page, the newest first.</summary>
    Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken);

    /// <summary>Every rite's number and day, the newest first: what the archive counts and indexes by month.</summary>
    Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken);

    /// <summary>Every rite's words, the newest first, without scores, votes or tags: what the search index needs.</summary>
    Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken);

    /// <summary>
    /// The saved rite, with the next number; <see cref="RiteRepository.DayAlreadyHasRite"/> when that day already has one,
    /// so two generations for the same day can't both save.
    /// </summary>
    Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a waiting draft as that day's rite, with the next number, and marks the draft published, all together.
    /// <see cref="RiteRepository.DayAlreadyHasRite"/> when the day has a rite; <see cref="RiteRepository.DraftNotWaiting"/>
    /// when the draft was published (or changed) meanwhile.
    /// </summary>
    Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken);

    /// <summary>Replaces any older scores of the rite.</summary>
    Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken);

    Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken);

    /// <summary>A page of the rites with this tag, the newest first.</summary>
    Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken);

    /// <summary>The rites judged before tags existed: with scores, without tags.</summary>
    Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken);
}
