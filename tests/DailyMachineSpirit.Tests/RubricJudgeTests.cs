using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Augury's quality (#10): the model that didn't write a draft scores it against a rubric, a few times.</summary>
public sealed class RubricJudgeTests : IDisposable
{
    private const string Judge = BacklogTestbed.QualityJudge;
    private const string OtherJudge = BacklogTestbed.SecondQualityJudge;

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 19, 21, 0, 0, TimeSpan.Zero));
    private readonly MetricsProbe probe = new();

    public void Dispose() => probe.Dispose();

    // No model judges its own writing.
    [Theory]
    [InlineData("gpt-6-sol", Judge)]
    [InlineData(Judge, OtherJudge)]
    public async Task ADraft_IsJudgedByAModelThatDidntWriteIt(string writer, string judge)
    {
        var models = new FakeChatClients().Otherwise(Judge, Score(8)).Otherwise(OtherJudge, Score(8));

        Ok(await Rubric(models).Judge(Draft(writer), CancellationToken.None));

        Assert.All(models.Requests, request => Assert.Equal(judge, request.Model));
        Assert.Equal(BacklogTestbed.QualityJudging.ScoresPerDraft, models.Requests.Count);
    }

    // A rewrite keeps the first writer's name, and a Scribe's words are nobody's: an amended draft is scored by every judge
    // in turn, so no model's taste for its own writing decides it.
    [Fact]
    public async Task AnAmendedDraft_IsScoredByEveryJudgeInTurn()
    {
        var models = new FakeChatClients().Otherwise(Judge, Score(8)).Otherwise(OtherJudge, Score(8));
        var amended = Draft(Judge) with { Writing = Draft(Judge).Writing with { Edited = true } };

        Ok(await Rubric(models).Judge(amended, CancellationToken.None));

        Assert.Equal([Judge, Judge, OtherJudge], models.Requests.Select(request => request.Model).Order());
    }

    // From 0 (a score of 1) to 1 (a 10): the average of the scores, which vary from one ask to the next.
    [Fact]
    public async Task TheQuality_IsTheScoresAverage_OnAScaleFromZeroToOne()
    {
        var models = new FakeChatClients().Answers(Judge, Score(4), Score(7), Score(10));

        var augury = Ok(await Rubric(models).Judge(Draft("gpt-6-sol"), CancellationToken.None));

        Assert.Equal(6f / 9, augury.Quality, 0.0001f);
        Assert.Equal(RubricJudge.Version, augury.JudgedBy);
        Assert.Equal(time.GetUtcNow().UtcDateTime, augury.JudgedAtUtc);
    }

    // A model that fails, or answers outside the scale (an empty answer reads as 0), gives no score: the others count.
    [Fact]
    public async Task AScoreThatDidntComeBack_IsLeftOut()
    {
        var models = new FakeChatClients().Answers(Judge, Score(0), Score(10)).Fails(Judge, 1);

        var augury = Ok(await Rubric(models).Judge(Draft("gpt-6-sol"), CancellationToken.None));

        Assert.Equal(1f, augury.Quality);
    }

    // Without a score, the error says why: for the logs.
    [Fact]
    public async Task NoScore_IsAnError_ThatSaysWhy()
    {
        var models = new FakeChatClients().Fails(Judge, BacklogTestbed.QualityJudging.ScoresPerDraft);

        var error = Failed(await Rubric(models).Judge(Draft("gpt-6-sol"), CancellationToken.None));

        Assert.Contains("overloaded", error.Message);
    }

    // Jev's scores and tags, and the rubric's quality, together; either failing, the draft is judged again later, whole.
    [Fact]
    public async Task TheRiteJudge_GivesJevsScoresAndTags_AndTheRubricsQuality()
    {
        var models = new FakeChatClients().Otherwise(Judge, Score(10));

        var judged = Ok(await RiteJudge(FakeJev.AnsweringBuildsAndRepetition(), models).Judge(Draft("gpt-6-sol"), CancellationToken.None));

        Assert.True(judged.Similarity.IsSome);
        Assert.Equal(Some(new[] { "builds" }), judged.Tags);
        Assert.Equal(Some(1f), judged.Augury.Map(augury => augury.Quality));
    }

    [Fact]
    public async Task TheRiteJudge_WithJevFailing_JudgesNothing()
    {
        var models = new FakeChatClients().Otherwise(Judge, Score(10));

        Failed(await RiteJudge(new FakeJev(HttpStatusCode.InternalServerError, ""), models).Judge(Draft("gpt-6-sol"), CancellationToken.None));
    }

    [Fact]
    public async Task TheRiteJudge_WithTheRubricFailing_JudgesNothing()
    {
        var models = new FakeChatClients().Fails(Judge, BacklogTestbed.QualityJudging.ScoresPerDraft);

        Failed(await RiteJudge(FakeJev.AnsweringBuildsAndRepetition(), models).Judge(Draft("gpt-6-sol"), CancellationToken.None));
    }

    private RubricJudge Rubric(FakeChatClients models)
        => new(models, Options.Create(BacklogTestbed.QualityJudging), time, probe.Metrics);

    private RiteJudge RiteJudge(FakeJev jev, FakeChatClients models) => new(jev.NewJudge(time, FakeJev.ApiKey), Rubric(models));

    private static string Score(int score) => $$"""{"score": {{score}}, "reason": "Scored {{score}}."}""";

    private static Draft Draft(string writer) => new()
    {
        Id = Guid.NewGuid(),
        State = DraftState.Waiting,
        Writing = new RiteWriting
        {
            Kind = RiteKind.Ritual,
            Title = "The Rite of Re-Run",
            Text = "Run it again, O Machine Spirit, until it passes.",
            HereticalTruth = "A test that passes on a rerun is flaky: find the race, don't pray to it.",
            GeneratedByModel = writer,
        },
    };
}
