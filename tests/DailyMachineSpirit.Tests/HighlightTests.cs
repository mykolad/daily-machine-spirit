using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class HighlightTests
{
    [Theory]
    [InlineData("The Warm CACHE caches", new[] { "cache" }, "The Warm <mark>CACHE</mark> <mark>cache</mark>s")]
    [InlineData("A cached rite", new[] { "cache", "cached" }, "A <mark>cached</mark> rite")]
    [InlineData("Rerun it", new[] { "re", "run" }, "<mark>Rerun</mark> it")]
    [InlineData("Press Re-run thrice", new[] { "re-run", "thrice" }, "Press <mark>Re-run</mark> <mark>thrice</mark>")]
    [InlineData("Nothing here", new[] { "cache" }, "Nothing here")]
    [InlineData("No words", new string[0], "No words")]
    public void Of_MarksEveryOccurrence_IgnoringCase_JoiningOverlaps(string text, string[] words, string expected)
        => Assert.Equal(expected, Highlight.Of(text, words));

    [Fact]
    public void Of_EncodesTheText_AndWhatItMarks()
        => Assert.Equal("a &lt;b&gt; <mark>&lt;cache&gt;</mark>", Highlight.Of("a <b> <cache>", ["<cache>"]));
}
