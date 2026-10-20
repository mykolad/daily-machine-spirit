using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// The order the waiting drafts are published in, first next. The drafts the Scribes placed come first, in their order.
/// The Augury orders the rest: better drafts first, but a draft much like the rites just before it waits, and prayers
/// and rituals take turns where the qualities allow, so the days vary. Drafts without a quality (judging failing)
/// follow, oldest first, and so do drafts judged by an older judge: a quality is only comparable with the same judge's.
/// </summary>
public static class LiturgicalCalendar
{
    /// <param name="recent">Published rites, newest first (only the first <see cref="AuguryOptions.RecentRitesCompared"/> count).</param>
    /// <param name="placed">The Scribes' order; drafts in it that aren't waiting any more are skipped.</param>
    public static List<Draft> Order(IReadOnlyList<Draft> waiting, IReadOnlyList<Rite> recent, IReadOnlyList<Guid> placed, AuguryOptions augury)
    {
        var waitingById = waiting.ToDictionary(draft => draft.Id);
        var ordered = placed.Distinct().Where(waitingById.ContainsKey).Select(id => waitingById[id]).ToList();
        var placedIds = ordered.Select(draft => draft.Id).ToHashSet();
        var unplaced = waiting.Where(draft => !placedIds.Contains(draft.Id)).ToList();

        // Oldest first: each pick joins the end, as the rite before the next one.
        var before = recent.Take(augury.RecentRitesCompared).Reverse().Select(rite => (rite.Writing.Kind, rite.Similarity))
            .Concat(ordered.Select(draft => (draft.Writing.Kind, draft.Similarity)))
            .ToList();
        var judge = NewestJudge(waiting);
        bool IsJudged(Draft draft) => JudgedBy(draft, judge);
        // A fixed order to start from, so equal values always resolve the same way.
        var judged = unplaced.Where(IsJudged).OrderBy(draft => draft.Writing.GeneratedAtUtc).ThenBy(draft => draft.Id).ToList();

        while (judged.Count > 0)
        {
            // The sort is stable, so of equal values the earlier draft wins.
            var next = judged.OrderByDescending(draft => Value(draft, before, augury)).First();
            judged.Remove(next);
            ordered.Add(next);
            before.Add((next.Writing.Kind, next.Similarity));
        }

        ordered.AddRange(unplaced.Where(draft => !IsJudged(draft)).OrderBy(draft => draft.Writing.GeneratedAtUtc).ThenBy(draft => draft.Id));
        return ordered;
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

    private static float Value(Draft draft, List<(RiteKind Kind, Option<RiteSimilarity> Similarity)> before, AuguryOptions augury)
    {
        var quality = draft.Augury.Map(judgment => judgment.Quality).IfNone(0f);
        var resemblance = before.TakeLast(augury.RecentRitesCompared)
            .Select(rite => Resemblance(draft.Similarity, rite.Similarity))
            .DefaultIfEmpty(0f)
            .Max();
        var sameKind = before.Count > 0 && before[^1].Kind == draft.Writing.Kind ? augury.SameKindPenalty : 0f;
        return quality - augury.ResemblancePenalty * resemblance - sameKind;
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
