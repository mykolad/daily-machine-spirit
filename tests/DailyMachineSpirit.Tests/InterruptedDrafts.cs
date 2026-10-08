using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The real drafts, with something happening once, right after the first read of the waiting ones.</summary>
public sealed class InterruptedDrafts : IDraftRepository
{
    private readonly IDraftRepository inner;
    private Option<Func<Task>> interruption;

    public InterruptedDrafts(IDraftRepository inner, Func<Task> interruption)
    {
        this.inner = inner;
        this.interruption = interruption;
    }

    public Task<Either<Error, Draft>> Add(Draft draft, CancellationToken cancellationToken)
        => inner.Add(draft, cancellationToken);

    public Task<Either<Error, Option<Draft>>> Get(Guid draftId, CancellationToken cancellationToken)
        => inner.Get(draftId, cancellationToken);

    public async Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken)
    {
        var waiting = await inner.GetWaiting(cancellationToken);
        var pending = interruption;
        interruption = None;
        await pending.IfSomeAsync(interrupt => interrupt());
        return waiting;
    }
}
