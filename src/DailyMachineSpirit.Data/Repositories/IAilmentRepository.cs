using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The rites and drafts written before rites had an ailment, and saving the one written for them.</summary>
public interface IAilmentRepository
{
    Task<Either<Error, List<WordsWithoutAilment>>> GetRitesAndUnpublishedDraftsWithoutAilment(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveIfUnchangedSinceRead(WordsWithoutAilment words, string ailment, CancellationToken cancellationToken);
}
