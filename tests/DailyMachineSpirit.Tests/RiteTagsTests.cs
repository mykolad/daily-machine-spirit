using DailyMachineSpirit.Functions.Tags;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class RiteTagsTests
{
    [Fact]
    public void Of_GivesTheTags_InTheJudgesOrder_LeavingOutKeysNoLongerInTheVocabulary()
    {
        var tags = RiteTags.Of(Some(new[] { "testing", "retired-tag", "ai" }));

        Assert.Equal(["Testing", "AI"], tags.Select(tag => tag.Label));
    }

    [Fact]
    public void Of_AnUntaggedRite_IsNoTags()
    {
        Assert.Empty(RiteTags.Of(None));
    }

    // A tag's key is part of its page's address, and its label is what readers see.
    [Fact]
    public void EveryTag_HasAnAddressFitKey_AndALabel()
    {
        Assert.All(RiteTags.All, tag => Assert.Matches("^[a-z]+(-[a-z]+)*$", tag.Key));
        Assert.All(RiteTags.All, tag => Assert.False(string.IsNullOrWhiteSpace(tag.Label)));
        Assert.Equal(RiteTags.All.Count, RiteTags.All.Select(tag => tag.Key).Distinct().Count());
        Assert.Equal(Some("Version control"), RiteTags.Find("version-control").Map(tag => tag.Label));
        Assert.True(RiteTags.Find("other").IsNone);
    }
}
