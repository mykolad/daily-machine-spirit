using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public sealed class SearchWordsTests
{
    [Fact]
    public void From_TakesEachWordOnce_WithoutQuotes()
    {
        Assert.Equal(["sleep", "5", "Cache"], SearchWords.From("  \"sleep 5\"   `Cache` cache 'sleep' "));
    }

    [Fact]
    public void From_TakesAtMostSixWords()
    {
        Assert.Equal(["a", "b", "c", "d", "e", "f"], SearchWords.From("a b c d e f g h"));
    }

    [Fact]
    public void From_QuotesAlone_AreNoWords()
    {
        Assert.Empty(SearchWords.From(" \"\" `` "));
    }

    [Fact]
    public void Typed_IsTrimmed_AndCutToItsLimit()
    {
        Assert.Equal("re-run", SearchWords.Typed("  re-run "));
        Assert.Equal("", SearchWords.Typed(null));
        Assert.Equal(SearchWords.MaxLength, SearchWords.Typed(new string('a', 300)).Length);
    }
}
