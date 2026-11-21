using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// The order the waiting drafts are published in, first next. The drafts the Scribes placed come first, in their order.
/// The Augury orders the rest: better drafts first, but a draft much like the rites just before it waits, and prayers
/// and rituals take turns where the qualities allow, as do the forms (a litany, a vigil), so the days vary. Drafts
/// without a quality (judging failing) follow, oldest first, and so do drafts judged by an older judge: a quality is only
/// comparable with the same judge's.
/// </summary>
public static class LiturgicalCalendar
{
    /// <param name="recent">Published rites, newest first (only the first <see cref="AuguryOptions.RecentRitesCompared"/> count).</param>
    /// <param name="placed">The Scribes' order; drafts in it that aren't waiting any more are skipped.</param>
    public static List<Draft> Order(IReadOnlyList<Draft> waiting, IReadOnlyList<Rite> recent, IReadOnlyList<Guid> placed, AuguryOptions augury)
        => Schedule(waiting, recent, placed, augury, new Dictionary<int, Draft>()).Select(entry => entry.Draft).ToList();

    /// <summary>
    /// The order day by day (day 0 the first), with drafts kept for feasts on their days (#202): each joins the rites
    /// before the next pick, as it will have been published by then, so the order around a feast is the one the
    /// publisher will choose. A day with nothing to fill (only feasts left, further on) goes without.
    /// </summary>
    /// <param name="waiting">The drafts the order may place: not those kept for feasts.</param>
    /// <param name="fixedDays">The feasts' drafts, by their day.</param>
    public static List<(Draft Draft, int Day)> Schedule(
        IReadOnlyList<Draft> waiting, IReadOnlyList<Rite> recent, IReadOnlyList<Guid> placed, AuguryOptions augury, IReadOnlyDictionary<int, Draft> fixedDays)
    {
        var waitingById = waiting.ToDictionary(draft => draft.Id);
        var anointed = new Queue<Draft>(placed.Distinct().Where(waitingById.ContainsKey).Select(id => waitingById[id]));
        var placedIds = anointed.Select(draft => draft.Id).ToHashSet();
        var unplaced = waiting.Where(draft => !placedIds.Contains(draft.Id)).ToList();

        // Oldest first: each pick joins the end, as the rite before the next one.
        var before = recent.Take(augury.RecentRitesCompared).Reverse().Select(rite => Before(rite.Writing, rite.Similarity)).ToList();
        // A fixed order to start from, so equal values always resolve the same way.
        var pool = unplaced.OrderBy(draft => draft.Writing.GeneratedAtUtc).ThenBy(draft => draft.Id).ToList();
        var lastFixed = fixedDays.Keys.DefaultIfEmpty(-1).Max();

        var days = new List<(Draft Draft, int Day)>();
        for (var day = 0; anointed.Count + pool.Count > 0 || day <= lastFixed; day++)
        {
            var next = fixedDays.TryGetValue(day, out var feast) ? Some(feast)
                : anointed.Count > 0 ? Some(anointed.Dequeue())
                : pool.Count > 0 ? Some(Pick(pool, StillWaiting(anointed, pool, fixedDays, day), before, augury))
                : None;
            next.IfSome(draft =>
            {
                days.Add((draft, day));
                before.Add(Before(draft.Writing, draft.Similarity));
            });
        }
        return days;
    }

    // What the publisher will find waiting on the day: the rest of the orders, and the feasts still to come.
    private static List<Draft> StillWaiting(Queue<Draft> anointed, List<Draft> pool, IReadOnlyDictionary<int, Draft> fixedDays, int day)
        => [.. anointed, .. pool, .. fixedDays.Where(fixedDay => fixedDay.Key > day).Select(fixedDay => fixedDay.Value)];

    // The day's pick from the pool, as the publisher will make it then: by quality among the drafts the newest judge of
    // those still waiting scored, or else the oldest. The judge is found anew each day, as one draft going out (a
    // feast's, say) can leave an older judge the newest.
    private static Draft Pick(
        List<Draft> pool, List<Draft> stillWaiting, List<(RiteKind Kind, Option<string> Form, Option<RiteSimilarity> Similarity)> before, AuguryOptions augury)
    {
        var judge = NewestJudge(stillWaiting);
        var judged = pool.Where(draft => JudgedBy(draft, judge)).ToList();
        // The sort is stable, so of equal values the earlier draft wins.
        var next = judged.Count > 0 ? judged.OrderByDescending(draft => Value(draft, before, augury)).First() : pool[0];
        pool.Remove(next);
        return next;
    }

    /// <summary>
    /// Whether the Augury ranks the draft by its quality: only when the most recent judge scored it. After a new Jev
    /// model or quality question, drafts it hasn't judged yet wait behind the ones it has, by age.
    /// </summary>
    public static bool RanksByQuality(Draft draft, IReadOnlyList<Draft> waiting) => JudgedBy(draft, NewestJudge(waiting));

    private static Option<string> NewestJudge(IReadOnlyList<Draft> waiting)
        => waiting.SelectMany(draft => draft.Augury).OrderByDescending(augury => augury.JudgedAtUtc)
            .Select(augury => augury.JudgedBy).HeadOrNone();

    private static bool JudgedBy(Draft draft, Option<string> judge)
        => draft.Augury.Exists(augury => judge.Exists(newest => newest == augury.JudgedBy));

    private static (RiteKind Kind, Option<string> Form, Option<RiteSimilarity> Similarity) Before(RiteWriting writing, Option<RiteSimilarity> similarity)
        => (writing.Kind, RiteForms.Of(writing.Form, writing.Title).Map(form => form.Key), similarity);

    private static float Value(Draft draft, List<(RiteKind Kind, Option<string> Form, Option<RiteSimilarity> Similarity)> before, AuguryOptions augury)
    {
        var quality = draft.Augury.Map(judgment => judgment.Quality).IfNone(0f);
        var week = before.TakeLast(augury.RecentRitesCompared).ToList();
        var resemblance = week
            .Select(rite => Resemblance(draft.Similarity, rite.Similarity))
            .DefaultIfEmpty(0f)
            .Max();
        var sameKind = before.Count > 0 && before[^1].Kind == draft.Writing.Kind ? augury.SameKindPenalty : 0f;
        // The more of the week's rites took the draft's form, the more it waits (#193): all of them, the whole penalty.
        var form = RiteForms.Of(draft.Writing.Form, draft.Writing.Title).Map(found => found.Key);
        var sameForm = week.Count == 0 || form.IsNone ? 0f : augury.SameFormPenalty * week.Count(rite => rite.Form == form) / week.Count;
        return quality - augury.ResemblancePenalty * resemblance - sameKind - sameForm;
    }

    // Cosine similarity of the scores: 1 for the same subject and act, 0 for nothing in common. Unknown, or from
    // different generators, counts as nothing in common.
    private static float Resemblance(Option<RiteSimilarity> first, Option<RiteSimilarity> second)
        => first.Bind(a => second
                .Filter(b => a.ScoresGeneratorVersion == b.ScoresGeneratorVersion && a.Scores.Length == b.Scores.Length)
                .Map(b => Cosine(a.Scores, b.Scores)))
            .IfNone(0f);

    private static float Cosine(float[] a, float[] b)
    {
        var dot = 0f;
        var lengthA = 0f;
        var lengthB = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            lengthA += a[i] * a[i];
            lengthB += b[i] * b[i];
        }
        return lengthA == 0 || lengthB == 0 ? 0f : dot / MathF.Sqrt(lengthA * lengthB);
    }
}
