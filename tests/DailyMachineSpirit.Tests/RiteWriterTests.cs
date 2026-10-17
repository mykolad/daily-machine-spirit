using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Writing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class RiteWriterTests : IDisposable
{
    private const string Sol = "gpt-6-sol";
    private const string Luna = "gpt-6-luna";

    private static readonly string GoodAnswer = FakeChatClients.Answer(
        "The Rite of Re-Run", "Press Re-run thrice, O Machine Spirit.", "The test is flaky: fix its race instead.");

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));
    private readonly FakeChatClients models = new();
    private readonly FakePrecepts precepts = new();
    private readonly MetricsProbe probe = new();

    public void Dispose() => probe.Dispose();

    [Fact]
    public async Task Write_TakesTheFirstModelsAnswer()
    {
        models.Answers(Sol, FakeChatClients.Answer("  The Rite of Re-Run ", " Press Re-run thrice. ", " It's flaky. "));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Writing.Title);
        Assert.Equal("Press Re-run thrice.", draft.Writing.Text);
        Assert.Equal("It's flaky.", draft.Writing.HereticalTruth);
        Assert.Equal(DraftState.Waiting, draft.State);
        Assert.Equal(RiteKind.Ritual, draft.Writing.Kind);
        Assert.Equal(Sol, draft.Writing.GeneratedByModel);
        Assert.Equal(time.GetUtcNow().UtcDateTime, draft.Writing.GeneratedAtUtc);
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
        // The far future forgot the AI, and its rites are about habits anyone knows, not a tool's flags.
        Assert.Contains("The AI is long gone, forbidden and forgotten", instructions);
        Assert.Contains("without flags", instructions);
        Assert.DoesNotContain("flattering an AI", instructions);
        Assert.DoesNotContain("--no-verify", instructions);
        Assert.Equal("Write a prayer.", messages.Single(message => message.Role == ChatRole.User).Text);
    }

    // So drafts improve at the source, not only in the ranking (#12): a rewrite follows them too.
    [Fact]
    public async Task WriteAndRewrite_FollowThePrecepts()
    {
        precepts.Hold("Prefer rites whose Heretical Truth names the real fix.", "Avoid `node_modules` this month.");
        models.Answers(Sol, GoodAnswer, GoodAnswer);

        await Writer(retryDelaySeconds: 0).Write(RiteKind.Prayer, [], CancellationToken.None);
        await Writer(retryDelaySeconds: 0).Rewrite(
            RiteKind.Prayer, new RiteContent("The Rite of Re-Run", "Press Re-run.", "It's flaky."), "Shorter.", CancellationToken.None);

        Assert.All(models.Requests, request =>
        {
            var instructions = request.Messages.Single(message => message.Role == ChatRole.System).Text;
            Assert.Contains("Follow their Precepts", instructions);
            Assert.Contains("  * Prefer rites whose Heretical Truth names the real fix.", instructions);
            Assert.Contains("  * Avoid `node_modules` this month.", instructions);
        });
    }

    // Writing matters more than following the Precepts: unreadable, the rite is written without them.
    [Fact]
    public async Task Write_GoesOnWithoutThePrecepts_WhenTheyCantBeRead()
    {
        precepts.Stored = LanguageExt.Common.Error.New("Cosmos is down.");
        models.Answers(Sol, GoodAnswer);

        Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Prayer, [], CancellationToken.None));

        Assert.DoesNotContain("Precepts", models.Requests.Single().Messages.Single(message => message.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task Write_DropsBackticksFromTheTitle_RatherThanAskAgain()
    {
        models.Answers(Sol, FakeChatClients.Answer("The Rite of `--force`", "Text.", "Truth."));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of --force", draft.Writing.Title);
        Assert.Single(models.Requests);
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

        Assert.Equal("The Rite of Re-Run", draft.Writing.Title);
        Assert.Equal(2, models.Requests.Count);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("text")]
    [InlineData("hereticalTruth")]
    public async Task Write_AsksAgain_RatherThanCutAnAnswerThatIsTooLong(string field)
    {
        var tooLong = new string('a', RiteWriting.MaxTextLength + 1);
        models.Answers(Sol, FakeChatClients.Answer(
            field == "title" ? tooLong : "Title", field == "text" ? tooLong : "Text.", field == "hereticalTruth" ? tooLong : "Truth."),
            GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Writing.Title);
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
    [InlineData(RiteKind.Ritual, "Title", "Lay sleep 5 upon the altar.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Title", "Text.", "Delete node_modules, then wait.")]
    // What the far future forgot.
    [InlineData(RiteKind.Prayer, "Title", "Text.", "Flattery does not make an AI's patch reliable.")]
    [InlineData(RiteKind.Ritual, "Title", "Whisper to the language model thrice.", "Truth.")]
    [InlineData(RiteKind.Ritual, "The Rite of the Chatbot", "Text.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Title", "Text.", "LLMs guess; read what they wrote.")]
    [InlineData(RiteKind.Ritual, "Title", "The vibe-coded build obeys no one.", "Truth.")]
    [InlineData(RiteKind.Prayer, "Title", "Text.", "Artificial intelligence won't read the logs for you.")]
    public async Task Write_AsksAgain_WhenTheAnswerBreaksTheRules(RiteKind kind, string title, string text, string hereticalTruth)
    {
        models.Answers(Sol, FakeChatClients.Answer(title, text, hereticalTruth), GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(kind, [], CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", draft.Writing.Title);
    }

    // Whole words, and only what the far future forgot: the machine's own words stay.
    [Theory]
    [InlineData("He said the cache would maintain itself.")]
    [InlineData("The data model drifted from the schema.")]
    [InlineData("Wait for the command prompt to return.")]
    [InlineData("The rain on the server room roof.")]
    public async Task Write_KeepsWordsTheFarFutureStillKnows(string truth)
    {
        models.Answers(Sol, FakeChatClients.Answer("Title", "Text.", truth));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal(truth, draft.Writing.HereticalTruth);
    }

    [Fact]
    public async Task Write_AllowsOneOmnissiah()
    {
        models.Answers(Sol, FakeChatClients.Answer("Title", "O Omnissiah, let the build pass.", "Truth."));

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Prayer, [], CancellationToken.None));

        Assert.Equal("O Omnissiah, let the build pass.", draft.Writing.Text);
    }

    [Fact]
    public async Task Write_FallsBackToTheNextModel_AfterTheFirstKeepsFailing()
    {
        models.Fails(Sol, 3).Answers(Luna, GoodAnswer);

        var draft = Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal(Luna, draft.Writing.GeneratedByModel);
        Assert.Equal([Sol, Sol, Sol, Luna], models.Requests.Select(request => request.Model));
    }

    [Fact]
    public async Task Write_CountsEveryAnswer_ByModelAndOutcome()
    {
        using var answers = probe.Collect<long>("dms.generation.answers");
        models.Fails(Sol, 1)
            .Answers(Sol, FakeChatClients.Answer("A Warhammer of Builds", "Text.", "Truth."), "not json at all")
            .Answers(Luna, GoodAnswer);

        Ok(await Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], CancellationToken.None));

        Assert.Equal(
            [$"model={Sol} outcome=failed", $"model={Sol} outcome=refused", $"model={Sol} outcome=failed", $"model={Luna} outcome=accepted"],
            MetricsProbe.Tags(answers, "model", "outcome"));
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

        Assert.Equal("The Rite of Re-Run", draft.Writing.Title);
        Assert.Equal(2, models.Requests.Count);
    }

    [Fact]
    public async Task Write_WhenCancelled_Throws()
    {
        models.Answers(Sol, GoodAnswer);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Writer(retryDelaySeconds: 0).Write(RiteKind.Ritual, [], new CancellationToken(canceled: true)));
    }

    // A Scribe waits for a rewrite: each model is asked once, without pausing, rather than three times 30 seconds apart.
    [Fact]
    public async Task Rewrite_AsksEachModelOnce_WithoutPausing()
    {
        models.Fails(Sol, 1).Answers(Luna, GoodAnswer);

        var rewritten = Ok(await Writer(retryDelaySeconds: 30).Rewrite(
            RiteKind.Ritual, new RiteContent("The Rite of Re-Run", "Press Re-run.", "It's flaky."), "Name the race.", CancellationToken.None));

        Assert.Equal("The Rite of Re-Run", rewritten.Title);
        Assert.Equal([Sol, Luna], models.Requests.Select(request => request.Model));
    }

    // A model that hasn't answered in time gives way to the next, so the Scribe's page comes back.
    [Fact]
    public async Task Rewrite_GivesUpOnAModelThatTakesTooLong()
    {
        models.Then(Sol, [() =>
        {
            time.Advance(TimeSpan.FromSeconds(30));
            return new TaskCompletionSource<string>().Task;
        }]);
        models.Answers(Luna, GoodAnswer);

        var rewritten = await Writer(retryDelaySeconds: 0).Rewrite(
            RiteKind.Ritual, new RiteContent("The Rite of Re-Run", "Press Re-run.", "It's flaky."), "Name the race.", CancellationToken.None);

        Assert.Equal("The Rite of Re-Run", Ok(rewritten).Title);
    }

    private RiteWriter Writer(int retryDelaySeconds)
        => new(models, precepts, Options.Create(new GenerationOptions { RetryDelaySeconds = retryDelaySeconds }), time, probe.Metrics, NullLogger<RiteWriter>.Instance);
}
