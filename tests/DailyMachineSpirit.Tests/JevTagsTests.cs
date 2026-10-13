using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Tests;

public sealed class JevTagsTests
{
    [Fact]
    public void From_TagsTheLikelyTopics_LikeliestFirst()
    {
        var tags = Tags(TopicScores.About(("caches", 0.3f), ("infrastructure", 0.6f), ("restarts", 0.1f)));

        Assert.Equal(["infrastructure", "caches"], tags);
    }

    [Fact]
    public void From_TagsAtMostTwo()
    {
        var tags = Tags(TopicScores.About(("builds", 0.3f), ("testing", 0.4f), ("ai", 0.3f)));

        Assert.Equal(2, tags.Length);
        Assert.Equal("testing", tags[0]);
    }

    [Fact]
    public void From_NeverTagsNoneOfTheAbove_AndUsesTheSitesKeys()
    {
        var tags = Tags(TopicScores.About(("other", 0.6f), ("version_control", 0.4f)));

        Assert.Equal(["version-control"], tags);
    }

    [Fact]
    public void From_ReadsOnlyJevsScoresForTheseQuestions()
    {
        var anotherQuestionSet = TopicScores.About(("builds", 1f)) with { ScoresGeneratorVersion = "jev-1.13.0/q2" };
        var anotherJudge = TopicScores.About(("builds", 1f)) with { ScoresGeneratorVersion = "other/q1" };
        var tooFewScores = new RiteSimilarity { ScoresGeneratorVersion = JevQuestions.Version("jev-1.13.0"), Scores = [1f, 0f] };

        Assert.True(JevTags.From(anotherQuestionSet).IsNone);
        Assert.True(JevTags.From(anotherJudge).IsNone);
        Assert.True(JevTags.From(tooFewScores).IsNone);
    }

    // Every Jev topic is one of the site's tags, so no rite loses a subject on the way.
    [Fact]
    public void EveryTopicButNoneOfTheAbove_IsOneOfTheSitesTags()
    {
        var topics = JevQuestions.Topics.Select(topic => topic.Key).Where(topic => topic != "other").ToList();

        Assert.Equal(topics.Order(), JevTags.TopicTags.Keys.Order());
        Assert.All(JevTags.TopicTags.Values, tag => Assert.True(RiteTags.Find(tag).IsSome, tag));
    }

    private static string[] Tags(RiteSimilarity similarity)
        => JevTags.From(similarity).IfNone(() => throw new Xunit.Sdk.XunitException("Expected Jev's scores to be read."));
}
