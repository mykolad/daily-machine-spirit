namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Which rites "More rites" offers: the ones whose similarity scores lie closest to the rite's own (cosine similarity
/// of the scores, which Jev makes from what a rite is about and what it asks for).
/// </summary>
public static class RelatedRites
{
    public const int Shown = 3;

    /// <param name="scoresByNumber">Every rite's scores from the same generator version as <paramref name="scores"/>.</param>
    public static List<int> Closest(int number, float[] scores, IReadOnlyDictionary<int, float[]> scoresByNumber)
        => scoresByNumber
            .Where(other => other.Key != number && other.Value.Length == scores.Length)
            .OrderByDescending(other => Cosine(scores, other.Value))
            .ThenByDescending(other => other.Key)
            .Take(Shown)
            .Select(other => other.Key)
            .ToList();

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, lengthA = 0, lengthB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            lengthA += a[i] * a[i];
            lengthB += b[i] * b[i];
        }
        return lengthA == 0 || lengthB == 0 ? 0 : dot / Math.Sqrt(lengthA * lengthB);
    }
}
