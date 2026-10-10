using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// The Augury's order of the waiting drafts: the first is published next. Better drafts go first, but a draft much like
/// the rites just before it waits, and prayers and rituals take turns where the qualities allow, so the days vary.
/// Drafts without a quality (Jev off or failing) follow, oldest first, and so do drafts an older judge scored: a quality
/// is only comparable with the same judge's.
/// </summary>
public static class LiturgicalCalendar
{
    /// <summary>How many rites before a draft its resemblance is measured against: a week of reading.</summary>
    public const int ResemblanceWindow = 7;

    // Weighed against quality (0 to 1). A near copy of a rite in the window loses half a point: it has to be much better
    // to go next. Following the same kind loses a little, enough to decide between drafts of similar quality.
    private const float ResemblancePenalty = 0.5f;
    private const float SameKindPenalty = 0.1f;

    /// <param name="recent">Published rites, newest first (only the first <see cref="ResemblanceWindow"/> count).</param>
    public static List<Draft> Order(IReadOnlyList<Draft> waiting, IReadOnlyList<Rite> recent)
    {
        // Oldest first: each pick joins the end, as the rite before the next one.
        var before = recent.Take(ResemblanceWindow).Reverse().Select(rite => (rite.Kind, rite.Similarity)).ToList();
        // The judge that scored most recently: after a new Jev model or quality question, drafts it hasn't judged yet
        // wait behind the ones it has.
        var judge = waiting.SelectMany(draft => draft.Augury).OrderByDescending(augury => augury.JudgedAtUtc)
            .Select(augury => augury.JudgedBy).HeadOrNone();
        bool IsJudged(Draft draft) => draft.Augury.Exists(augury => judge.Exists(newest => newest == augury.JudgedBy));
        // A fixed order to start from, so equal values always resolve the same way.
        var judged = waiting.Where(IsJudged).OrderBy(draft => draft.GeneratedAtUtc).ThenBy(draft => draft.Id).ToList();

        var ordered = new List<Draft>();
        while (judged.Count > 0)
        {
            // The sort is stable, so of equal values the earlier draft wins.
            var next = judged.OrderByDescending(draft => Value(draft, before)).First();
            judged.Remove(next);
            ordered.Add(next);
            before.Add((next.Kind, next.Similarity));
        }

        ordered.AddRange(waiting.Where(draft => !IsJudged(draft)).OrderBy(draft => draft.GeneratedAtUtc).ThenBy(draft => draft.Id));
        return ordered;
    }

    private static float Value(Draft draft, List<(RiteKind Kind, Option<RiteSimilarity> Similarity)> before)
    {
        var quality = draft.Augury.Map(augury => augury.Quality).IfNone(0f);
        var resemblance = before.TakeLast(ResemblanceWindow)
            .Select(rite => Resemblance(draft.Similarity, rite.Similarity))
            .DefaultIfEmpty(0f)
            .Max();
        var sameKind = before.Count > 0 && before[^1].Kind == draft.Kind ? SameKindPenalty : 0f;
        return quality - ResemblancePenalty * resemblance - sameKind;
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
