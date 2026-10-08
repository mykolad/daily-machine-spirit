using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Writing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public class RiteWriterTests
{
    private const string Sol = "gpt-6-sol";
    private const string Luna = "gpt-6-luna";

    private static readonly string GoodAnswer = FakeChatClients.Answer(
        "The Rite of Re-Run", "Press Re-run thrice, O Machine Spirit.", "The test is flaky: fix its race instead.");

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));
    private readonly FakeChatClients models = new();

    [Fact]
    public async Task Write_TakesTheFirstModelsAnswer()
    {
        models.Answers(Sol, FakeChatClients.Answer("  The Rite of Re-Run ", " Press Re-run thrice. ", " It's flaky. "));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Title);
        Assert.Equal("Press Re-run thrice.", draft.Text);
        Assert.Equal("It's flaky.", draft.HereticalTruth);
        Assert.Equal(DraftState.Waiting, draft.State);
        Assert.Equal(RiteKind.Ritual, draft.Kind);
        Assert.Equal(Sol, draft.GeneratedByModel);
        Assert.Equal(time.GetUtcNow().UtcDateTime, draft.GeneratedAtUtc);
        Assert.Single(models.Requests);
    }

    [Fact]
    public async Task Write_AsksForTheKind_AndListsTheTitlesToAvoid()
    {
        models.Answers(Sol, GoodAnswer);

        await Writer(retryDelaySeconds: 0).Write(RiteKind.Prayer, ["Litany of the Clean Cache", "The Rite of Re-Run"], CancellationToken.None);

        var messages = models.Requests.Single().Messages;
        var instructions = messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("Write one PRAYER", instructions);
        Assert.Contains("* Litany of the Clean Cache", instructions);
        Assert.Contains("* The Rite of Re-Run", instructions);
        Assert.Contains("Never quote or paraphrase Games Workshop", instructions);
        Assert.Equal("Write a prayer.", messages.Single(message => message.Role == ChatRole.User).Text);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"title": "Only a title"}""")]
    [InlineData("""{"title": null, "text": "Text.", "hereticalTruth": "Truth."}""")]
    [InlineData("""{"title": "  ", "text": "Text.", "hereticalTruth": "Truth."}""")]
    public async Task Write_AsksAgain_WhenTheAnswerIsUnusable(string unusable)
    {
        models.Answers(Sol, unusable, GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Title);
        Assert.Equal(2, models.Requests.Count);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("text")]
    [InlineData("hereticalTruth")]
    public async Task Write_AsksAgain_RatherThanCutAnAnswerThatIsTooLong(string field)
    {
        var tooLong = new string('a', Rite.MaxTextLength + 1);
        models.Answers(Sol, FakeChatClients.Answer(
            field == "title" ? tooLong : "Title", field == "text" ? tooLong : "Text.", field == "hereticalTruth" ? tooLong : "Truth."),
            GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Title);
        Assert.Equal(2, models.Requests.Count);
    }

    [Theory]
    [InlineData(RiteKind.Ritual, "A Warhammer of Builds", "Text.", "Truth.")]
    [InlineData(RiteKind.Ritual, "Title", "Praise the adeptus of caching.", "Truth.")]
    [InlineData(RiteKind.Ritual, "Litany of the Tyranids", "Text.", "Truth.")]
    [InlineData(RiteKind.Prayer, "The Necron Rite", "Text.", "Truth.")]
    [InlineData(RiteKind.Ritual, "Title", "Call on the Omnissiah, then rebuild.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Title", "O Omnissiah, hear me. O Omnissiah, hear me.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Litany of the Omnissiah", "Text.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Title", "Text.", "The Omnissiah won't fix a race.")]
    public async Task Write_AsksAgain_WhenTheAnswerUsesForbiddenNames(RiteKind kind, string title, string text, string hereticalTruth)
    {
        models.Answers(Sol, FakeChatClients.Answer(title, text, hereticalTruth), GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(kind, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Title);
    }

    [Fact]
    public async Task Write_AllowsOneOmnissiah()
    {
        models.Answers(Sol, FakeChatClients.Answer("Title", "O Omnissiah, let the build pass.", "Truth."));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Prayer, [], CancellationToken.None));

        Assert.Equal("O Omnissiah, let the build pass.", draft.Text);
    }

    [Fact]
    public async Task Write_FallsBackToTheNextModel_AfterTheFirstKeepsFailing()
    {
        models.Fails(Sol, 3).Answers(Luna, GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal(Luna, draft.GeneratedByModel);
        Assert.Equal([Sol, Sol, Sol, Luna], models.Requests.Select(request => request.Model));
    }

    [Fact]
    public async Task Write_WhenEveryModelFails_ReturnsAnError()
    {
        models.Fails(Sol, 3).Fails(Luna, 3);

        var result = await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None);

        Assert.Contains("gpt-6-luna, try 3: The model is overloaded.", Failed(result).Message);
        Assert.Equal(6, models.Requests.Count);
    }

    [Fact]
    public async Task Write_WaitsBeforeAskingAgain()
    {
        models.Fails(Sol, 1).Answers(Sol, GoodAnswer);

        var writing = Writer(retryDelaySeconds: 30).Write(RiteKind.Ritual, [], CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(29));
        Assert.Single(models.Requests);
        time.Advance(TimeSpan.FromSeconds(1));
        var draft = Ok(await writing);

        Assert.Equal("The Rite of Re-Run", draft.Title);
        Assert.Equal(2, models.Requests.Count);
    }

    [Fact]
    public async Task Write_WhenCancelled_Throws()
    {
        models.Answers(Sol, GoodAnswer);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], new CancellationToken(canceled: true)));
    }

    private RiteWriter Writer(int retryDelaySeconds)
        => new(models, Options.Create(new GenerationOptions { RetryDelaySeconds = retryDelaySeconds }), time, NullLogger<RiteWriter>.Instance);

}
