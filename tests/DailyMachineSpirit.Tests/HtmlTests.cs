using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class HtmlTests
{
    [Theory]
    [InlineData("Delete `node_modules` and pray.", "Delete <code>node_modules</code> and pray.")]
    [InlineData("`a` and `b`", "<code>a</code> and <code>b</code>")]
    [InlineData("A stray ` stays", "A stray ` stays")]
    [InlineData("`one` and a stray `", "<code>one</code> and a stray `")]
    [InlineData("No code at all.", "No code at all.")]
    public void WithInlineCode_TurnsBacktickedSpansIntoCode(string text, string expected)
        => Assert.Equal(expected, Html.WithInlineCode(text));

    [Fact]
    public void WithInlineCode_EncodesWhatTheModelWrote()
        => Assert.Equal("&lt;script&gt; and <code>&lt;b&gt;</code>", Html.WithInlineCode("<script> and `<b>`"));

    [Fact]
    public void FirstLine_EndsAtTheFirstSentenceAfterFortyCharacters()
        => Assert.Equal(
            "Short. Then a much longer sentence follows here.",
            Html.FirstLine("Short. Then a much longer sentence follows here. And another one."));

    [Fact]
    public void FirstLine_IgnoresAFullStopInsideAWord()
        => Assert.Equal(
            "Run the cache-clearing rite with node.js beside you, then wait!",
            Html.FirstLine("Run the cache-clearing rite with node.js beside you, then wait! It returns."));

    [Fact]
    public void FirstLine_OfOneSentence_IsTheWholeText()
        => Assert.Equal("A single sentence without an end", Html.FirstLine("A single sentence without an end"));

    [Fact]
    public void Dates_ReadAsTheDesignShowsThem()
    {
        var day = new DateOnly(2026, 10, 9);

        Assert.Equal("9 October 2026", Html.LongDate(day));
        Assert.Equal("9 Oct 2026", Html.ShortDate(day));
        Assert.Equal("2026-10-09", Html.IsoDate(day));
    }
}
