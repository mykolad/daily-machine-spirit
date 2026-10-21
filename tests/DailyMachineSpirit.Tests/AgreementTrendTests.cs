using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;
using Microsoft.Extensions.Options;
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
    // places; anointing its second after it leaves it there, at #2; and humbling the first back to second moves it one
    // from the Augury's third: 1 on average.
    [Fact]
    public async Task AMonth_CountsTheTopPicksBurned_AndHowFarDraftsMoved()
    {
        var best = await testbed.AddDraft("Best", 0.9f);
        await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.5f);
        var worst = await testbed.AddDraft("Worst", 0.3f);
        var scribes = testbed.Scribes();
        Ok(await scribes.Burn(best.Id, "", Tester, CancellationToken.None));
        Ok(await scribes.Anoint(worst.Id, "", Tester, CancellationToken.None));
        Ok(await scribes.Anoint(fair.Id, "", Tester, CancellationToken.None));
        Ok(await scribes.Humble(worst.Id, "", Tester, CancellationToken.None));
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));

        var months = Ok(await new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time).Months(CancellationToken.None));

        Assert.Equal(Settings.ScriptoriumPages.AgreementMonthsShown, months.Count);
        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 6, 1)],
            [months[0].FirstDayUtc, months[1].FirstDayUtc, months[^1].FirstDayUtc]);
        Assert.Equal(new AgreementMonth(new DateOnly(2026, 11, 1), 0, 0, 0, 0, None), months[0]);
        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 4, 1, 1, 3, Some(1.0)), months[1]);
    }

    // A decision saved before the place after a move was kept: an anointing then made the draft next.
    [Fact]
    public async Task AnAnointingSavedBeforeItsPlaceAfterWasKept_CountsAsMakingTheDraftNext()
    {
        var anointing = new ScribeDecision
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Action = ScribeAction.Anoint,
            Title = "Anointed long ago",
            CalendarPlace = Some(3),
            AuguryPlace = Some(3),
            AuguryRankedByQuality = true,
            DecidedAtUtc = testbed.Time.GetUtcNow().UtcDateTime,
        };
        Ok(await testbed.Scriptorium.SavePlacements(new Placements([], new Dictionary<Guid, string>(), None),
            Some(anointing), [], AuditEntries.Of(testbed.Time, Tester, AuditAction.Anoint, None, None), CancellationToken.None));

        var months = Ok(await new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time).Months(CancellationToken.None));

        Assert.Equal(Some(2.0), months[0].AveragePlacesFromAuguryPlace);
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

        var october = Ok(await new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time).Months(CancellationToken.None))[0];

        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 2, 1, 0, 0, None), october);
    }

    // After a new judge, the drafts an older one scored wait behind, by age, as unjudged ones do: the Augury placed them
    // second and third, but burning or moving them says nothing about its judgment.
    [Fact]
    public async Task DraftsAnOlderJudgeScored_CountOnlyAsJudgments()
    {
        var judgedEarlier = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        await JudgedBy("jev-1.13.0/quality-q1", judgedEarlier.AddDays(1), "Current", 0.5f);
        var older = await JudgedBy("jev-1.12.0/quality-q1", judgedEarlier, "Older", 0.9f);
        var olderStill = await JudgedBy("jev-1.12.0/quality-q1", judgedEarlier, "Older still", 0.8f);
        Ok(await testbed.Scribes().Anoint(olderStill.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(older.Id, "", Tester, CancellationToken.None));

        var october = Ok(await new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time).Months(CancellationToken.None))[0];

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

        var october = Ok(await new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time).Months(CancellationToken.None))[0];

        Assert.Equal(new AgreementMonth(new DateOnly(2026, 10, 1), 2, 1, 0, 0, None), october);
    }

    // A draft judging couldn't score: it waits, placed by age.
    private Task<Draft> Unjudged(string title) => Waiting(title, None);

    private Task<Draft> JudgedBy(string judge, DateTime judgedAtUtc, string title, float quality)
        => Waiting(title, new Augury { Quality = quality, JudgedBy = judge, JudgedAtUtc = judgedAtUtc });

    private async Task<Draft> Waiting(string title, Option<Augury> augury)
        => Ok(await testbed.Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Augury = augury,
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
