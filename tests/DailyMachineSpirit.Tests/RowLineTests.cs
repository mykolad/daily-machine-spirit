using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class RowLineTests
{
    // Thirty "word," six characters apart: the last space before the 84th character is the 13th.
    private static readonly string Words = string.Join(" ", Enumerable.Repeat("word,", 30));

    [Fact]
    public void Opening_EndsAtTheLastWordBefore84Characters_WithoutItsPunctuation()
        => Assert.Equal($"{string.Join(" ", Enumerable.Repeat("word,", 14)).TrimEnd(',')}…", RowLine.Opening(Rite(Words, "Truth.")));

    [Fact]
    public void Opening_OfAShortText_IsTheWholeText_WithoutItsBackticks()
        => Assert.Equal("Delete node_modules and pray.", RowLine.Opening(Rite("Delete `node_modules` and pray.", "Truth.")));

    [Fact]
    public void For_AMatchFarIntoTheText_ShowsTheWordsAroundIt()
    {
        var text = $"{Words} the cache was warm {Words}";

        var (line, fromTruth) = RowLine.For(Rite(text, "Truth."), ["CACHE"]);

        Assert.False(fromTruth);
        Assert.StartsWith("…", line);
        Assert.EndsWith("…", line);
        Assert.Contains("the cache was warm", line);
        Assert.True(line.Length <= 30 + "cache".Length + 50 + 2, line);
    }

    [Fact]
    public void For_AMatchNearTheStart_ShowsTheOpeningWords()
    {
        var (line, _) = RowLine.For(Rite($"The cache {Words}", "Truth."), ["cache"]);

        Assert.StartsWith("The cache word", line);
        Assert.EndsWith("…", line);
    }

    [Fact]
    public void For_AMatchInTheTitleAlone_ShowsTheOpeningWords()
        => Assert.Equal(("Delete node_modules and pray.", false), RowLine.For(Rite("Delete `node_modules` and pray.", "It was stale.", "The Warm Cache"), ["cache"]));

    [Fact]
    public void For_AMatchInTheTruthAlone_ShowsTheTruthsWords()
        => Assert.Equal(("Your cache was stale.", true), RowLine.For(Rite("Delete `node_modules` and pray.", "Your `cache` was stale."), ["cache"]));

    private static Rite Rite(string text, string truth) => Rite(text, truth, "The Vigil");

    private static Rite Rite(string text, string truth, string title)
        => new() { Writing = new RiteWriting { Title = title, Text = text, HereticalTruth = truth } };
}
