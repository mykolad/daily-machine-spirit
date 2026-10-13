using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Tags;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class RiteTagsTests
{
    [Fact]
    public void Of_TagsTheLikelyTopics_LikeliestFirst()
    {
        var tags = RiteTags.Of(TopicScores.About(("caches", 0.3f), ("infrastructure", 0.6f), ("restarts", 0.1f)));

        Assert.Equal(["infrastructure", "caches"], tags.Select(tag => tag.Key));
    }

    [Fact]
    public void Of_TagsAtMostTwo()
    {
        var tags = RiteTags.Of(TopicScores.About(("builds", 0.3f), ("testing", 0.4f), ("ai", 0.3f)));

        Assert.Equal(2, tags.Count);
        Assert.Equal("testing", tags[0].Key);
    }

    [Fact]
    public void Of_NeverTagsNoneOfTheAbove()
    {
        var tags = RiteTags.Of(TopicScores.About(("other", 0.7f), ("data", 0.3f)));

        Assert.Equal(["data"], tags.Select(tag => tag.Key));
    }

    [Fact]
    public void Of_GivesNoTags_WithoutJevsTopicScores()
    {
        var anotherQuestionSet = TopicScores.About(("builds", 1f)) with { ScoresGeneratorVersion = "jev-1.13.0/q2" };
        var anotherJudge = TopicScores.About(("builds", 1f)) with { ScoresGeneratorVersion = "other/q1" };
        var tooFewScores = new RiteSimilarity { ScoresGeneratorVersion = JevQuestions.Version("jev-1.13.0"), Scores = [1f, 0f] };

        Assert.Empty(RiteTags.Of(anotherQuestionSet));
        Assert.Empty(RiteTags.Of(anotherJudge));
        Assert.Empty(RiteTags.Of(tooFewScores));
        Assert.Empty(RiteTags.Of(None));
    }

    // A tag's key is part of its page's address, and its label is what readers see.
    [Fact]
    public void EveryTopicButNoneOfTheAbove_IsATag_WithAnAddressAndALabel()
    {
        Assert.Equal(JevQuestions.Topics.Count - 1, RiteTags.All.Count);
        Assert.All(RiteTags.All, tag => Assert.Matches("^[a-z]+(-[a-z]+)*$", tag.Key));
        Assert.Equal(RiteTags.All.Count, RiteTags.All.Select(tag => tag.Key).Distinct().Count());
        Assert.Equal(Some("Version control"), RiteTags.Find("version-control").Map(tag => tag.Label));
        Assert.True(RiteTags.Find("other").IsNone);
    }
}
