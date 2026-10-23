using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Making a waiting draft the day's rite: the one change that gives the site a new rite to show.</summary>
public interface IRitePublishing
{
    /// <summary>
    /// Publishes a waiting draft as that day's rite, with the next number, and marks the draft published, all together.
    /// <see cref="RiteRepository.DayAlreadyHasRite"/> when the day has a rite; <see cref="RiteRepository.DraftNotWaiting"/>
    /// when the draft was published (or changed) meanwhile.
    /// </summary>
    Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken);
}
