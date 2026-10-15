using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class RiteRepositoryTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Add_SavesTheRite_ThenFindsItByNumberAndDay()
    {
        var date = new DateOnly(2026, 10, 7);

        var saved = await AddOrFail(MakeRite(date, "The Rite of Re-Run"));

        var byNumber = Ok(await Repository.GetByNumbers([saved.Number], CancellationToken.None)).HeadOrNone();
        var byDay = Ok(await Repository.GetPublishedOn(date, CancellationToken.None));
        Assert.Equal(Some("The Rite of Re-Run"), byNumber.Map(rite => rite.Writing.Title));
        Assert.Equal(Some(RiteKind.Ritual), byNumber.Map(rite => rite.Writing.Kind));
        Assert.Equal(Some("Press Re-run thrice, intoning `it passed locally`."), byDay.Map(rite => rite.Writing.Text));
        Assert.Equal(Some(saved.Number), byDay.Map(rite => rite.Number));
    }

    [Fact]
    public async Task Add_NumbersRitesOneAfterAnother()
    {
        var first = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "First"));
        var second = await AddOrFail(MakeRite(new DateOnly(2026, 10, 8), "Second"));

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    [Fact]
    public async Task Add_ForADayThatHasARite_SavesNothing_AndUsesNoNumber()
    {
        var date = new DateOnly(2026, 10, 7);
        await AddOrFail(MakeRite(date, "First"));

        var second = await Repository.Add(MakeRite(date, "Second"), CancellationToken.None);
        var next = await AddOrFail(MakeRite(date.AddDays(1), "Next day"));

        Assert.Equal(Left<Error, Rite>(RiteRepository.DayAlreadyHasRite), second);
        Assert.Equal("First", (await PublishedOnOrFail(date)).Writing.Title);
        Assert.Equal(2, next.Number);
    }

    [Fact]
    public async Task Add_ManyAtOnce_GivesEachDayItsOwnNumber()
    {
        var rites = Enumerable.Range(1, 4).Select(day => MakeRite(new DateOnly(2026, 10, day), $"Day {day}"));

        var saved = await Task.WhenAll(rites.Select(AddOrFail));

        Assert.Equal([1, 2, 3, 4], saved.Select(rite => rite.Number).Order());
    }

    [Fact]
    public async Task GetByNumbers_AndGetPublishedOn_FindNothingWhenMissing()
    {
        Assert.Empty(Ok(await Repository.GetByNumbers([42], CancellationToken.None)));
        Assert.Equal(Option<Rite>.None, Ok(await Repository.GetPublishedOn(new DateOnly(2026, 1, 1), CancellationToken.None)));
    }

    [Fact]
    public async Task GetPublishedOn_ADocumentWithANullTitle_IsAnErrorInsteadOfANull()
    {
        var date = new DateOnly(2026, 10, 7);
        await cosmos.Container.CreateItemAsync(new
        {
            id = "2026-10-07", partition = "rites", type = "rite", number = 1, publishedOnUtc = date, kind = "prayer",
            title = (string?)null, text = "", hereticalTruth = "", generatedByModel = "", generatedAtUtc = DateTime.UtcNow,
        });

        var result = await Repository.GetPublishedOn(date, CancellationToken.None);

        Assert.True(result.IsLeft);
    }

    [Fact]
    public async Task GetNewest_ReturnsTheNewestFirst()
    {
        foreach (var day in new[] { 3, 5, 4 })
        {
            await AddOrFail(MakeRite(new DateOnly(2026, 10, day), $"Day {day}"));
        }

        var newest = Ok(await Repository.GetNewest(2, CancellationToken.None));

        Assert.Equal(["Day 5", "Day 4"], newest.Select(rite => rite.Writing.Title));
    }

    [Fact]
    public async Task GetDays_ListsEveryRitesNumberAndDay_NewestFirst()
    {
        var first = await AddOrFail(MakeRite(new DateOnly(2026, 9, 30), "September"));
        var second = await AddOrFail(MakeRite(new DateOnly(2026, 10, 1), "October"));

        var days = Ok(await Repository.GetDays(CancellationToken.None));

        Assert.Equal([new RiteDay(second.Number, new DateOnly(2026, 10, 1)), new RiteDay(first.Number, new DateOnly(2026, 9, 30))], days);
    }

    [Fact]
    public async Task Timestamps_ComeBackAsUtc_EvenWhenGivenAsLocalTime()
    {
        var utc = new DateTime(2026, 10, 7, 0, 0, 5, DateTimeKind.Utc);
        var timed = MakeRite(new DateOnly(2026, 10, 7), "Timed");
        var rite = timed with { Writing = timed.Writing with { GeneratedAtUtc = utc.ToLocalTime() } };
        await AddOrFail(rite);
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        Ok(await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [1f], CreatedAtUtc = scoredAt.ToLocalTime() },
            CancellationToken.None));

        var saved = await PublishedOnOrFail(rite.PublishedOnUtc);

        Assert.Equal(utc, saved.Writing.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.Writing.GeneratedAtUtc.Kind);
        Assert.Equal(Some(scoredAt), saved.Similarity.Map(similarity => similarity.CreatedAtUtc));
        Assert.Equal(Some(DateTimeKind.Utc), saved.Similarity.Map(similarity => similarity.CreatedAtUtc.Kind));
    }

    [Fact]
    public async Task Add_WithScoresAlreadyAttached_StoresTheirTimestampAsUtc()
    {
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        var rite = MakeRite(new DateOnly(2026, 10, 7), "Scored first") with
        {
            Similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [0.5f], CreatedAtUtc = scoredAt.ToLocalTime() },
        };

        await AddOrFail(rite);
        var saved = await PublishedOnOrFail(rite.PublishedOnUtc);

        Assert.Equal(Some(scoredAt), saved.Similarity.Map(similarity => similarity.CreatedAtUtc));
        Assert.Equal(Some(DateTimeKind.Utc), saved.Similarity.Map(similarity => similarity.CreatedAtUtc.Kind));
    }

    [Fact]
    public async Task SaveScores_StoresTheScoresExactly_AndReplacesOlderOnes()
    {
        var rite = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Scored"));
        var createdAt = new DateTime(2026, 10, 7, 0, 1, 0, DateTimeKind.Utc);

        Ok(await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/old", Scores = [1f, 0f], CreatedAtUtc = createdAt }, CancellationToken.None));
        Ok(await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/new", Scores = [0.25f, 0.5f, 0.1f], CreatedAtUtc = createdAt }, CancellationToken.None));

        Assert.Empty(Ok(await Repository.GetScoresByRiteNumber("jev/old", CancellationToken.None)));
        var scores = Ok(await Repository.GetScoresByRiteNumber("jev/new", CancellationToken.None));
        Assert.Equal([0.25f, 0.5f, 0.1f], scores[rite.Number]);
    }

    [Fact]
    public async Task GetScoresByRiteNumber_SkipsRitesWithoutScores()
    {
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Unscored"));

        Assert.Empty(Ok(await Repository.GetScoresByRiteNumber("jev/v1", CancellationToken.None)));
    }

    [Fact]
    public async Task GetTagged_FindsTheTagsRites_NewestFirst_APageAtATime()
    {
        var first = await AddOrFail(MakeRite(new DateOnly(2026, 10, 5), "First") with { Tags = new[] { "caches", "restarts" } });
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 6), "Builds only") with { Tags = new[] { "builds" } });
        var third = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Third") with { Tags = new[] { "caches" } });
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 8), "Untagged"));

        var page = Ok(await Repository.GetTagged("caches", int.MaxValue, 10, CancellationToken.None));
        var older = Ok(await Repository.GetTagged("caches", third.Number, 10, CancellationToken.None));

        Assert.Equal([third.Number, first.Number], page.Select(rite => rite.Number));
        Assert.Equal(Some(new[] { "caches" }), page[0].Tags);
        Assert.Equal([first.Number], older.Select(rite => rite.Number));
    }

    [Fact]
    public async Task GetUntagged_FindsScoredRitesWithoutTags_UntilTheirTagsAreSaved()
    {
        var scored = await AddOrFail(MakeRite(new DateOnly(2026, 10, 5), "Scored"));
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 6), "Unscored"));
        var judged = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Judged with none") with { Tags = new string[0] });
        var similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = [1f], CreatedAtUtc = new DateTime(2026, 10, 5, 0, 1, 0, DateTimeKind.Utc) };
        Ok(await Repository.SaveScores(scored.PublishedOnUtc, similarity, CancellationToken.None));
        Ok(await Repository.SaveScores(judged.PublishedOnUtc, similarity, CancellationToken.None));

        var untagged = Ok(await Repository.GetUntagged(CancellationToken.None));
        Ok(await Repository.SaveTags(scored.PublishedOnUtc, ["data"], CancellationToken.None));
        var afterwards = Ok(await Repository.GetUntagged(CancellationToken.None));

        Assert.Equal([scored.Number], untagged.Select(rite => rite.Number));
        Assert.Empty(afterwards);
        Assert.Equal(Some(new[] { "data" }), Ok(await Repository.GetByNumbers([scored.Number], CancellationToken.None))[0].Tags);
    }

    [Fact]
    public async Task Search_FindsRitesHoldingEveryWord_InAnyPart_IgnoringCase_NewestFirst()
    {
        var inTitle = await AddOrFail(MakeRite(new DateOnly(2026, 10, 5), "The Litany of the Warm CACHE"));
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 6), "The Rite of Re-Run"));
        var inTruth = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "The Vigil") with
        {
            Writing = MakeRite(new DateOnly(2026, 10, 7), "The Vigil").Writing with { HereticalTruth = "Your cache was stale; re-run cleared it." },
        });

        var cache = Ok(await Repository.Search(["cache"], int.MaxValue, 10, CancellationToken.None));
        var both = Ok(await Repository.Search(["Cache", "STALE"], int.MaxValue, 10, CancellationToken.None));
        var older = Ok(await Repository.Search(["cache"], inTruth.Number, 10, CancellationToken.None));

        Assert.Equal([inTruth.Number, inTitle.Number], cache.Select(rite => rite.Number));
        Assert.Equal([inTruth.Number], both.Select(rite => rite.Number));
        Assert.Equal([inTitle.Number], older.Select(rite => rite.Number));
        Assert.Equal([new RiteDay(inTruth.Number, inTruth.PublishedOnUtc), new RiteDay(inTitle.Number, inTitle.PublishedOnUtc)], Ok(await Repository.FindDays(["cache"], CancellationToken.None)));
        Assert.Empty(Ok(await Repository.FindDays(["cache", "nowhere"], CancellationToken.None)));
    }

    // A reader's words are parameters: quotes and query syntax in them are only text to look for.
    [Fact]
    public async Task Search_TakesWordsAsTextOnly()
    {
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 5), "The Rite of Re-Run"));

        var found = Ok(await Repository.Search(["') OR true OR CONTAINS(c.title, '"], int.MaxValue, 10, CancellationToken.None));

        Assert.Empty(found);
    }

    [Fact]
    public async Task EveryRequest_IsMeasured_ByOperationAndStatusCode()
    {
        // The meter is shared with tests running alongside, so this looks for its own requests among theirs.
        using var charges = new MetricCollector<double>(null, CosmosMetricsHandler.MeterName, "dms.cosmos.request_charge");
        var date = new DateOnly(2026, 10, 7);

        await AddOrFail(MakeRite(date, "Measured"));
        Ok(await Repository.GetPublishedOn(date, CancellationToken.None));
        Ok(await Repository.GetPublishedOn(new DateOnly(2026, 1, 1), CancellationToken.None));
        Ok(await Repository.GetNewest(1, CancellationToken.None));

        var measured = charges.GetMeasurementSnapshot()
            .Select(measurement => (Operation: $"{measurement.Tags["operation"]} {measurement.Tags["status_code"]}", Charge: measurement.Value))
            .ToList();
        Assert.Contains(measured, request => request is { Operation: "read 200", Charge: > 0 });
        Assert.Contains(measured, request => request.Operation == "batch 200");
        Assert.Contains(measured, request => request.Operation == "read 404");
        Assert.Contains(measured, request => request.Operation == "query 200");
    }

    [Fact]
    public async Task EveryCall_ReturnsACosmosFailureAsAnError_InsteadOfThrowing()
    {
        // A container that doesn't exist: Cosmos answers every call with 404.
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance);
        var date = new DateOnly(2026, 10, 7);

        Assert.True((await missing.GetPublishedOn(date, CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetNewest(1, CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetOlderThan(5, 6, CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetByNumbers([1, 2], CancellationToken.None)).IsLeft);
        Assert.True((await missing.Add(MakeRite(date, "Lost"), CancellationToken.None)).IsLeft);
        Assert.True((await missing.SaveScores(date, new RiteSimilarity(), CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetScoresByRiteNumber("jev/v1", CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetTagged("caches", int.MaxValue, 6, CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetUntagged(CancellationToken.None)).IsLeft);
        Assert.True((await missing.SaveTags(date, ["caches"], CancellationToken.None)).IsLeft);
        Assert.True((await missing.Search(["cache"], int.MaxValue, 6, CancellationToken.None)).IsLeft);
        Assert.True((await missing.FindDays(["cache"], CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetDays(CancellationToken.None)).IsLeft);
    }

    [Fact]
    public async Task Cancellation_StillThrows()
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Repository.GetNewest(1, new CancellationToken(canceled: true)));

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private static Rite MakeRite(DateOnly date, string title) => new()
    {
        PublishedOnUtc = date,
        Writing = new RiteWriting
        {
            Kind = RiteKind.Ritual,
            Title = title,
            Text = "Press Re-run thrice, intoning `it passed locally`.",
            HereticalTruth = "Your test depends on timing.",
            GeneratedByModel = "gpt-6-sol",
            GeneratedAtUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        },
    };

    private async Task<Rite> AddOrFail(Rite rite) => Ok(await Repository.Add(rite, CancellationToken.None));

    private async Task<Rite> PublishedOnOrFail(DateOnly date)
        => Ok(await Repository.GetPublishedOn(date, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException($"Expected a rite on {date}."));
}
