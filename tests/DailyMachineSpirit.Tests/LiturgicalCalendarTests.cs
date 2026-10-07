using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation;
using LanguageExt;

namespace DailyMachineSpirit.Tests;

public class LiturgicalCalendarTests
{
    // Scores along two made-up dimensions: rites about the same thing point the same way.
    private static readonly float[] AboutCaches = [1f, 0f];
    private static readonly float[] AboutBuilds = [0f, 1f];

    [Fact]
    public void Order_PutsTheBetterDraftsFirst()
    {
        var weak = MakeDraft("Weak", RiteKind.Prayer, 0.2f, AboutCaches);
        var strong = MakeDraft("Strong", RiteKind.Prayer, 0.9f, AboutBuilds);

        var ordered = LiturgicalCalendar.Order([weak, strong], [], []);

        Assert.Equal(["Strong", "Weak"], Titles(ordered));
    }

    [Fact]
    public void Order_LetsADraftLikeTheRitesBeforeItWait()
    {
        var yesterday = MakeRite(RiteKind.Ritual, AboutCaches);
        var anotherCache = MakeDraft("Another cache", RiteKind.Prayer, 0.9f, AboutCaches);
        var aBuild = MakeDraft("A build", RiteKind.Prayer, 0.7f, AboutBuilds);

        var ordered = LiturgicalCalendar.Order([anotherCache, aBuild], [yesterday], []);

        Assert.Equal(["A build", "Another cache"], Titles(ordered));
    }

    [Fact]
    public void Order_SpreadsSimilarDrafts_ApartFromEachOther()
    {
        var cacheOne = MakeDraft("Cache one", RiteKind.Prayer, 0.9f, AboutCaches);
        var cacheTwo = MakeDraft("Cache two", RiteKind.Prayer, 0.85f, AboutCaches);
        var build = MakeDraft("Build", RiteKind.Prayer, 0.6f, AboutBuilds);

        var ordered = LiturgicalCalendar.Order([cacheOne, cacheTwo, build], [], []);

        Assert.Equal(["Cache one", "Build", "Cache two"], Titles(ordered));
    }

    [Fact]
    public void Order_AlternatesKinds_BetweenDraftsOfSimilarQuality()
    {
        var yesterday = MakeRite(RiteKind.Prayer, AboutBuilds);
        var prayer = MakeDraft("Prayer", RiteKind.Prayer, 0.75f, AboutCaches);
        var ritual = MakeDraft("Ritual", RiteKind.Ritual, 0.7f, AboutCaches);

        var ordered = LiturgicalCalendar.Order([prayer, ritual], [yesterday], []);

        Assert.Equal("Ritual", ordered[0].Title);
    }

    [Fact]
    public void Order_PutsDraftsWithoutAQualityLast_OldestFirst()
    {
        var newerUnjudged = MakeDraft("Newer, unjudged", RiteKind.Prayer, 0f, AboutCaches) with
        {
            Augury = Option<Augury>.None,
            GeneratedAtUtc = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
        };
        var olderUnjudged = newerUnjudged with { Id = Guid.NewGuid(), Title = "Older, unjudged", GeneratedAtUtc = newerUnjudged.GeneratedAtUtc.AddDays(-1) };
        var weak = MakeDraft("Weak but judged", RiteKind.Prayer, 0.1f, AboutBuilds);

        var ordered = LiturgicalCalendar.Order([newerUnjudged, olderUnjudged, weak], [], []);

        Assert.Equal(["Weak but judged", "Older, unjudged", "Newer, unjudged"], Titles(ordered));
    }

    [Fact]
    public void Order_IgnoresResemblance_ToScoresFromAnotherGenerator()
    {
        var yesterday = MakeRite(RiteKind.Ritual, AboutCaches) with
        {
            Similarity = new RiteSimilarity { ScoresGeneratorVersion = "older/q0", Scores = AboutCaches },
        };
        var anotherCache = MakeDraft("Another cache", RiteKind.Prayer, 0.9f, AboutCaches);
        var aBuild = MakeDraft("A build", RiteKind.Prayer, 0.7f, AboutBuilds);

        var ordered = LiturgicalCalendar.Order([anotherCache, aBuild], [yesterday], []);

        Assert.Equal(["Another cache", "A build"], Titles(ordered));
    }

    [Fact]
    public void Order_PutsDraftsAnOlderJudgeScored_AfterTheNewestJudges()
    {
        var judgedEarlier = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldJudge = MakeDraft("Old judge, high score", RiteKind.Prayer, 0.9f, AboutCaches) with
        {
            Augury = new Augury { Quality = 0.9f, JudgedBy = "jev-1.12.0/quality-q1", JudgedAtUtc = judgedEarlier },
        };
        var newJudge = MakeDraft("New judge, low score", RiteKind.Prayer, 0.3f, AboutBuilds) with
        {
            Augury = new Augury { Quality = 0.3f, JudgedBy = "jev-1.13.0/quality-q1", JudgedAtUtc = judgedEarlier.AddDays(1) },
        };

        var ordered = LiturgicalCalendar.Order([oldJudge, newJudge], [], []);

        Assert.Equal(["New judge, low score", "Old judge, high score"], Titles(ordered));
    }

    private static string[] Titles(List<Draft> ordered) => ordered.Select(draft => draft.Title).ToArray();

    private static Draft MakeDraft(string title, RiteKind kind, float quality, float[] scores) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Title = title,
        GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        Similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = scores },
        Augury = new Augury { Quality = quality, JudgedBy = "jev/quality-q1" },
    };

    private static Rite MakeRite(RiteKind kind, float[] scores) => new()
    {
        Kind = kind,
        Title = "Published",
        Similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = scores },
    };
}
