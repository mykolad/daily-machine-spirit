using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Whether the Augury is good enough to be left alone (#12): month by month, how many of its top picks the Scribes
/// burn and how far they move drafts from its places. When both stay low, the Scribes can step back.
/// </summary>
public sealed class AgreementTrend
{
    /// <summary>How many months the trend shows, this one included.</summary>
    public const int MonthsShown = 6;

    /// <summary>The Augury's top picks: the drafts it would publish in the next three days.</summary>
    public const int TopPicks = 3;

    private readonly IScriptoriumRepository scriptorium;
    private readonly TimeProvider time;

    public AgreementTrend(IScriptoriumRepository scriptorium, TimeProvider time)
    {
        this.scriptorium = scriptorium;
        this.time = time;
    }

    /// <summary>The last <see cref="MonthsShown"/> months, the newest first, including those without a judgment.</summary>
    public Task<Either<Error, List<AgreementMonth>>> Months(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var months = Enumerable.Range(0, MonthsShown).Select(back => new DateOnly(today.Year, today.Month, 1).AddMonths(-back)).ToList();
        return scriptorium.GetDecisionsSince(months[^1], cancellationToken)
            .MapAsync(decisions => Task.FromResult(months.Select(month => Of(month, decisions)).ToList()));
    }

    private static AgreementMonth Of(DateOnly month, IReadOnlyList<ScribeDecision> decisions)
    {
        var inMonth = decisions
            .Where(decision => decision.DecidedAtUtc.Year == month.Year && decision.DecidedAtUtc.Month == month.Month)
            .ToList();
        var burns = inMonth.Where(decision => decision.Action == ScribeAction.Burn).ToList();
        var distances = inMonth.Select(Distance).Somes().ToList();
        return new AgreementMonth(
            month,
            inMonth.Count,
            burns.Count,
            burns.Count(decision => decision.AuguryPlace.Exists(place => place <= TopPicks)),
            distances.Count,
            distances.Count == 0 ? None : Some(distances.Average()));
    }

    // How far a move took the draft from the Augury's place for it. Its new place follows from the action and the place
    // before; a draft the Augury hadn't placed (not judged) has no distance to tell.
    private static Option<double> Distance(ScribeDecision decision)
        => decision.CalendarPlace
            .Bind(before => NewPlace(decision.Action, before))
            .Bind(after => decision.AuguryPlace.Map(augury => (double)Math.Abs(after - augury)));

    private static Option<int> NewPlace(ScribeAction action, int before) => action switch
    {
        ScribeAction.Anoint => Some(1),
        ScribeAction.Exalt => Some(before - 1),
        ScribeAction.Humble => Some(before + 1),
        _ => None,
    };
}
