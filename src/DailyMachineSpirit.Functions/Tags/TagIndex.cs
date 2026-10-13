using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Tags;

/// <summary>
/// Which rites have each tag. Finding them takes every rite's scores, so the index is kept for <see cref="Lifetime"/>:
/// each instance reads the scores at most that often, however many readers open tag pages. A rite published meanwhile
/// joins its tags' pages within that time. A failed read isn't kept, so the next request tries again.
/// </summary>
public sealed class TagIndex
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IRiteRepository rites;
    private readonly TimeProvider time;
    // Replaced whole, never changed: a request on another thread sees the old index or the new one, both complete.
    private volatile Kept kept = new(new Dictionary<string, List<int>>(), DateTimeOffset.MinValue);

    public TagIndex(IRiteRepository rites, TimeProvider time)
    {
        this.rites = rites;
        this.time = time;
    }

    /// <summary>The numbers of the rites with this tag, the newest first.</summary>
    public async Task<Either<Error, List<int>>> Numbers(Tag tag, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var current = kept;
        if (now < current.Until)
        {
            return Lookup(current, tag);
        }

        return (await rites.GetSimilarities(cancellationToken)).Map(similarities =>
        {
            var fresh = new Kept(Index(similarities), now + Lifetime);
            kept = fresh;
            return Lookup(fresh, tag);
        });
    }

    private static Dictionary<string, List<int>> Index(Dictionary<int, RiteSimilarity> similarities)
        => similarities
            .SelectMany(rite => RiteTags.Of(rite.Value).Select(tag => (Tag: tag.Key, Number: rite.Key)))
            .GroupBy(tagged => tagged.Tag)
            .ToDictionary(group => group.Key, group => group.Select(tagged => tagged.Number).OrderDescending().ToList());

    private static List<int> Lookup(Kept index, Tag tag) => index.Numbers.TryGetValue(tag.Key, out var numbers) ? numbers : [];

    private sealed record Kept(Dictionary<string, List<int>> Numbers, DateTimeOffset Until);
}
