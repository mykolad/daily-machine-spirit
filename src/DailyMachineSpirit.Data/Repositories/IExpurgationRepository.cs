using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Every place a published rite's words are kept, so an Expurgator can erase some everywhere at once: the rite, the
/// draft it was published from, the Scribes' decisions on that draft (the Augury's lessons), the Record's entries about
/// it, and drafts marked as much like it. Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IExpurgationRepository
{
    /// <summary>The copies of the rite's words, its own first; None when no rite has that number.</summary>
    Task<Either<Error, Option<List<WordsKept>>>> FindWordsOf(int number, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the changed copies with the erasure's entry, in one batch: all or nothing.
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when any copy changed since it was read (a vote changes the
    /// rite's version too), <see cref="ExpurgationRepository.TooManyCopies"/> when they don't fit in one batch.
    /// </summary>
    Task<Either<Error, Unit>> Erase(IReadOnlyList<WordsKept> changed, AuditEntry audit, CancellationToken cancellationToken);
}
