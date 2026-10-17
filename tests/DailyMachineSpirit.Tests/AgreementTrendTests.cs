using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Whether the Scribes agree with the Augury (#12), month by month, against the Cosmos DB emulator.</summary>
public sealed class AgreementTrendTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // The Augury orders the four by quality. Burning its first pick counts against it; anointing its third moves it two
    // places, and humbling that one back from first to second moves it one from the Augury's third: 1.5 on average.
    [Fact]
    public async Task AMonth_CountsTheTopPicksBurned_AndHowFarDraftsMoved()
    {
        var best = await testbed.AddDraft("Best", 0.9f);
        await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.5f);
        var worst = await testbed.AddDraft("Worst", 0.3f);
        var scribes = testbed.Scribes();
        Ok(await scribes.Burn(best.Id, "", Tester, CancellationToken.None));
        Ok(await scribes.Anoint(worst.Id, "", Tester, CancellationToken.None));
        Ok(await scribes.Humble(worst.Id, "", Tester, CancellationToken.None));
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));

        var months = Ok(await new AgreementTrend(testbed.Scriptorium, testbed.Time).Months(CancellationToken.None));

        Assert.Equal(AgreementTrend.MonthsShown, months.Count);
        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 6, 1)],
            [months[0].FirstDayUtc, months[1].FirstDayUtc, months[^1].FirstDayUtc]);
        Assert.Equal(new AgreementMonth(new DateOnly(2026, 11, 1), 0, 0, 0, 0, None), months[0]);
        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 3, 1, 1, 2, Some(1.5)), months[1]);
    }

    // Without a quality (Jev off or failing), the Augury places drafts by age alone: burning or moving them says
    // nothing about its judgment, so they count only as judgments.
    [Fact]
    public async Task UnjudgedDrafts_CountOnlyAsJudgments()
    {
        var first = await Unjudged("First");
        var second = await Unjudged("Second");
        Ok(await testbed.Scribes().Anoint(second.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(first.Id, "", Tester, CancellationToken.None));

        var october = Ok(await new AgreementTrend(testbed.Scriptorium, testbed.Time).Months(CancellationToken.None))[0];

        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 2, 1, 0, 0, None), october);
    }

    // A burn of a draft the Augury placed below its top picks, or a restore, says nothing against it.
    [Fact]
    public async Task OtherJudgments_CountOnlyAsJudgments()
    {
        await testbed.AddDraft("Best", 0.9f);
        await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.5f);
        var worst = await testbed.AddDraft("Worst", 0.3f);
        Ok(await testbed.Scribes().Burn(worst.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Restore(worst.Id, "", Tester, CancellationToken.None));

        var october = Ok(await new AgreementTrend(testbed.Scriptorium, testbed.Time).Months(CancellationToken.None))[0];

        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 2, 1, 0, 0, None), october);
    }

    // A draft judging couldn't score: it waits, placed by age.
    private async Task<Draft> Unjudged(string title)
        => Ok(await testbed.Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Prayer,
                Title = title,
                Text = "O Machine Spirit, let the cache be warm.",
                HereticalTruth = "A cold cache is just a cache that hasn't been read yet.",
                GeneratedByModel = Sol,
                GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
