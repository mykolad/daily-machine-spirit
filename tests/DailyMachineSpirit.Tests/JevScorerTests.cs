using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public class JevScorerTests
{
    private static readonly Draft Draft = new()
    {
        Id = Guid.NewGuid(),
        Kind = RiteKind.Ritual,
        Title = "The Rite of Re-Run",
        Text = "Press Re-run thrice.",
        HereticalTruth = "The test is flaky.",
    };

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 0, 1, 0, TimeSpan.Zero));

    [Fact]
    public async Task Score_GivesTheTopicsThenTheActs_InQuestionOrder()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var scored = Ok(await jev.Scorer(time, FakeJev.ApiKey).Score(Draft, CancellationToken.None));

        var similarity = scored.Similarity.IfNone(() => throw new Xunit.Sdk.XunitException("Expected scores."));
        Assert.Equal(JevQuestions.Topics.Count + JevQuestions.Acts.Count, similarity.Scores.Length);
        Assert.Equal(1.0f, similarity.Scores[0]);
        Assert.Equal(0f, similarity.Scores[1]);
        var acts = similarity.Scores[JevQuestions.Topics.Count..];
        Assert.Equal(0.75f, acts[0]);
        Assert.Equal(0.25f, acts[JevQuestions.Acts.ToList().FindIndex(act => act.Key == "waiting")]);
        Assert.Equal("jev-1.13.0/q1", similarity.ScoresGeneratorVersion);
        Assert.Equal(time.GetUtcNow().UtcDateTime, similarity.CreatedAtUtc);
    }

    [Fact]
    public async Task Score_JudgesTheQuality_AsTheProbabilitiesWeightedAverage()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var scored = Ok(await jev.Scorer(time, FakeJev.ApiKey).Score(Draft, CancellationToken.None));

        var augury = scored.Augury.IfNone(() => throw new Xunit.Sdk.XunitException("Expected a quality."));
        // Half "excellent" (1) and half "good" (2/3).
        Assert.Equal(5f / 6, augury.Quality, 0.0001f);
        Assert.Equal("jev-1.13.0/quality-q1", augury.JudgedBy);
        Assert.Equal(time.GetUtcNow().UtcDateTime, augury.JudgedAtUtc);
        Assert.Equal(Draft.Title, scored.Title);
    }

    [Fact]
    public async Task Score_SendsTheDraftAndTheKey()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        await jev.Scorer(time, FakeJev.ApiKey).Score(Draft, CancellationToken.None);

        var (body, authorization) = Assert.Single(jev.Requests);
        Assert.Equal($"Bearer {FakeJev.ApiKey}", authorization);
        Assert.Equal("jev-1.13.0", body["model"]?.GetValue<string>());
        var state = body["state"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains(Draft.Title, state);
        Assert.Contains(Draft.Text, state);
        Assert.Contains(Draft.HereticalTruth, state);
        Assert.Equal(JevQuestions.Topics.Count, body["questions"]?["topic"]?["criteria"]?.AsObject().Count);
        Assert.Equal(JevQuestions.Qualities.Count, body["questions"]?["quality"]?["criteria"]?.AsObject().Count);
    }

    [Fact]
    public async Task Score_WithoutAKey_IsOff_AndAsksNothing()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var scored = Ok(await jev.Scorer(time, " ").Score(Draft, CancellationToken.None));

        Assert.Equal(Draft, scored);
        Assert.Empty(jev.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired, """{"error": "Out of credits."}""", "Jev answered 402")]
    [InlineData(HttpStatusCode.OK, """{"answers": {}}""", "no probabilities")]
    [InlineData(HttpStatusCode.OK, "<html>", "")]
    public async Task Score_WhenJevCantAnswer_ReturnsAnError(HttpStatusCode status, string body, string expected)
    {
        var jev = new FakeJev(status, body);

        var error = Failed(await jev.Scorer(time, FakeJev.ApiKey).Score(Draft, CancellationToken.None));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task Score_WhenCancelled_Throws()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => jev.Scorer(time, FakeJev.ApiKey).Score(Draft, new CancellationToken(canceled: true)));
    }
}
