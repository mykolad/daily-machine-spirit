using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The rites and drafts written before rites had an ailment, and saving the one written for them.</summary>
public interface IAilmentRepository
{
    /// <summary>Every published rite, and every draft not published, that has no ailment.</summary>
    Task<Either<Error, List<WordsWithoutAilment>>> GetWithout(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the ailment, only if the words are still the version read: <see cref="ScriptoriumRepository.ChangedMeanwhile"/>
    /// when they changed (an amendment, an erasure, a vote), since an ailment written from older words could hold an
    /// erased one.
    /// </summary>
    Task<Either<Error, Unit>> Save(WordsWithoutAilment words, string ailment, CancellationToken cancellationToken);
}
