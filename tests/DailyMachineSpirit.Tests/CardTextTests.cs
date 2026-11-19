using DailyMachineSpirit.Functions.ShareCards;

namespace DailyMachineSpirit.Tests;

// A rite's text as the share card sets it (#191): plain runs of characters, a pair of backticks the only rule.
public sealed class CardTextTests
{
    [Fact]
    public void Words_KeepCodeWhole_WithThePunctuationAfterIt()
        => Assert.Equal(
            ["invoke", "[sudo npm install],", "O", "Spirit."],
            Shown(CardText.Words("invoke `sudo npm install`, O  Spirit.")));

    [Fact]
    public void ABacktickWithoutItsPair_IsDrawnAsItIs()
        => Assert.Equal(["a", "`b", "c"], Shown(CardText.Words("a `b c")));

    [Fact]
    public void TwoBackticksWithNothingBetween_AreDrawnAsTheyAre()
        => Assert.Equal(["``"], Shown(CardText.Words("``")));

    [Fact]
    public void Markup_IsOnlyCharacters()
        => Assert.Equal(["<b>bold</b>", "*not*"], Shown(CardText.Words("<b>bold</b> *not*")));

    [Fact]
    public void Plain_DropsOnlyTheBackticksThatMadeCode()
        => Assert.Equal("Invoke sudo npm install, then `wait.", CardText.Plain("Invoke `sudo npm install`, then `wait."));

    // Each word as its runs, code in brackets.
    private static List<string> Shown(IReadOnlyList<IReadOnlyList<CardRun>> words)
        => [.. words.Select(word => string.Concat(word.Select(run => run.IsCode ? $"[{run.Text}]" : run.Text)))];
}
