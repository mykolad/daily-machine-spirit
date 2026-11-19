using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites a signed-in reader has witnessed (#215), kept with their voter key so every browser they sign in on shows
/// the same. Readers who don't sign in keep theirs in the browser alone.
/// </summary>
public interface IWitnessedRepository
{
    /// <summary>The reader's witnessed rites, lowest first; empty when they've kept none here.</summary>
    Task<Either<Error, IReadOnlyList<int>>> Get(string voterKey, CancellationToken cancellationToken);

    /// <summary>
    /// Adds rites to the reader's list and answers the whole list, lowest first. Two tabs adding at once both count. Past
    /// <see cref="WitnessedRepository.MaxRites"/>, the lowest numbers make room.
    /// </summary>
    Task<Either<Error, IReadOnlyList<int>>> Add(string voterKey, IReadOnlyCollection<int> rites, CancellationToken cancellationToken);

    /// <summary>Forgets the reader's list, so every browser they sign in on forgets it too.</summary>
    Task<Either<Error, Unit>> Forget(string voterKey, CancellationToken cancellationToken);
}
