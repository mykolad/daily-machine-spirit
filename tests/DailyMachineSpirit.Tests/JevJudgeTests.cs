using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public class JevJudgeTests
{
    private static readonly Draft Draft = new()
    {
        Id = Guid.NewGuid(),
        Writing = new RiteWriting
        {
            Kind = RiteKind.Ritual,
            Title = "The Rite of Re-Run",
            Text = "Press Re-run thrice.",
            HereticalTruth = "The test is flaky.",
        },
    };

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 0, 1, 0, TimeSpan.Zero));

    [Fact]
    public async Task Judge_GivesTheTopicsThenTheActs_InQuestionOrder()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var judged = Ok(await jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, CancellationToken.None));

        var similarity = judged.Similarity.IfNone(() => throw new Xunit.Sdk.XunitException("Expected scores."));
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
    public async Task Judge_TagsTheDraft_FromItsTopics()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var judged = Ok(await jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, CancellationToken.None));

        Assert.Equal(Some(new[] { "builds" }), judged.Tags);
    }

    // The Augury's quality is the rubric judge's (#10): Jev isn't asked for one.
    [Fact]
    public async Task Judge_GivesNoQuality()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var judged = Ok(await jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, CancellationToken.None));

        Assert.True(judged.Augury.IsNone);
        Assert.Null(Assert.Single(jev.Requests).Body["questions"]?["quality"]);
    }

    [Fact]
    public async Task Judge_SendsTheDraftAndTheKey()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        await jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, CancellationToken.None);

        var (body, authorization) = Assert.Single(jev.Requests);
        Assert.Equal($"Bearer {FakeJev.ApiKey}", authorization);
        Assert.Equal("jev-1.13.0", body["model"]?.GetValue<string>());
        var state = body["state"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains(Draft.Writing.Title, state);
        Assert.Contains(Draft.Writing.Text, state);
        Assert.Contains(Draft.Writing.HereticalTruth, state);
        Assert.Equal(JevQuestions.Topics.Count, body["questions"]?["topic"]?["criteria"]?.AsObject().Count);
    }

    [Fact]
    public async Task Judge_WithoutAKey_IsOff_AndAsksNothing()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        var judged = Ok(await jev.NewJudge(time, " ").Judge(Draft, CancellationToken.None));

        Assert.Equal(Draft, judged);
        Assert.Empty(jev.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired, """{"error": "Out of credits."}""", "Jev answered 402")]
    [InlineData(HttpStatusCode.OK, """{"answers": {}}""", "no probabilities")]
    [InlineData(HttpStatusCode.OK, "<html>", "")]
    public async Task Judge_WhenJevCantAnswer_ReturnsAnError(HttpStatusCode status, string body, string expected)
    {
        var jev = new FakeJev(status, body);

        var error = Failed(await jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, CancellationToken.None));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task Judge_WhenCancelled_Throws()
    {
        var jev = FakeJev.AnsweringBuildsAndRepetition();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => jev.NewJudge(time, FakeJev.ApiKey).Judge(Draft, new CancellationToken(canceled: true)));
    }
}
