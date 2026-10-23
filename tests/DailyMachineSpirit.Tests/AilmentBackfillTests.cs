using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Writing the ailment of the rites and drafts written before rites had one (#115), in the Cosmos DB emulator.</summary>
public sealed class AilmentBackfillTests : IAsyncLifetime
{
    private const string Luna = "gpt-6-luna";
    private static readonly DateOnly Day = new(2026, 10, 22);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero));
    private readonly MetricsProbe probe = new();
    private readonly FakeChatClients models = new();
    private readonly FakePageCache pages = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync()
    {
        probe.Dispose();
        return cosmos.DisposeAsync();
    }

    // Each from its own words; the rites API, which serves the published rites' ailments, is purged once.
    [Fact]
    public async Task Run_WritesTheAilment_OfOlderRitesAndDrafts_AndPurgesTheRitesApi()
    {
        await AddRite(Day, None);
        var waiting = await AddDraft(DraftState.Waiting, None);
        var burned = await AddDraft(DraftState.Burned, None);
        models.Otherwise(Luna, Answer("A build that only passes on its second run."));

        var written = Ok(await Backfill().Run(CancellationToken.None));

        Assert.Equal(3, written);
        Assert.Equal(Some("A build that only passes on its second run."), Ok(await Rites.GetByNumbers([1], CancellationToken.None))[0].Writing.Ailment);
        foreach (var draft in new[] { waiting, burned })
        {
            Assert.Equal(Some("A build that only passes on its second run."), Ok(await Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Writing.Ailment));
        }
        Assert.Equal([Paths.RitesApi], Assert.Single(pages.Purges));
        Assert.Contains("Rite of the Second Run", models.Requests[0].Messages.Last().Text);
        // Saved to published rites without a Scribe reading it first, so it keeps the rules every rite keeps.
        var instructions = models.Requests[0].Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("Never quote or paraphrase Games Workshop", instructions);
        Assert.Contains("Nothing abusive, hateful", instructions);
    }

    // Drafts only: the rites API shows no draft, so nothing is purged. One with an ailment already, or published (its rite
    // carries its words), isn't asked about.
    [Fact]
    public async Task Run_LeavesWhatHasAnAilment_AndPurgesNothingForDraftsAlone()
    {
        await AddDraft(DraftState.Waiting, Some("A cache that serves yesterday's page."));
        await AddDraft(DraftState.Published, None);
        var waiting = await AddDraft(DraftState.Waiting, None);
        models.Otherwise(Luna, Answer("A service that only works after a restart."));

        var written = Ok(await Backfill().Run(CancellationToken.None));

        Assert.Equal(1, written);
        Assert.Single(models.Requests);
        Assert.Equal(Some("A service that only works after a restart."), Ok(await Drafts.Get(waiting.Id, CancellationToken.None)).Bind(found => found.Writing.Ailment));
        Assert.Empty(pages.Purges);
    }

    // Amended between the read and the save: an ailment written from the old words could hold an erased one, or describe
    // words that are gone. The amendment's ailment stays.
    [Fact]
    public async Task SaveIfUnchangedSinceRead_WhenTheWordsChangedMeanwhile_LeavesThemAsTheyAre()
    {
        var draft = await AddDraft(DraftState.Waiting, None);
        var read = Assert.Single(Ok(await Ailments.GetRitesAndUnpublishedDraftsWithoutAilment(CancellationToken.None)));
        var amended = draft with { Writing = draft.Writing with { Title = "Amended", Ailment = Some("A Scribe's own ailment.") } };
        await cosmos.Container.UpsertItemAsync(DraftDocument.From(amended), CosmosCalls.SharedPartitionKey);

        var saved = await Ailments.SaveIfUnchangedSinceRead(read, "Written from the old words.", CancellationToken.None);

        saved.Match(Right: _ => Assert.Fail("The ailment was saved over the amendment."), Left: error => Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, error));
        Assert.Equal(Some("A Scribe's own ailment."), Ok(await Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Writing.Ailment));
    }

    // A model that fails, or answers what an ailment mustn't be, leaves the draft for the next start; the rest go on.
    [Fact]
    public async Task Run_SkipsWhatTheModelCantDescribe_AndGoesOn()
    {
        await AddDraft(DraftState.Waiting, None);
        await AddDraft(DraftState.Waiting, None);
        models.Answers(Luna, Answer("A chatbot gave the wrong answer."), Answer("A deploy that times out at random."));

        var written = Ok(await Backfill().Run(CancellationToken.None));

        Assert.Equal(1, written);
        Assert.Single(Ok(await Ailments.GetRitesAndUnpublishedDraftsWithoutAilment(CancellationToken.None)));
    }

    [Fact]
    public async Task Starting_WritesThem_AndAfterwardsNothingIsLeft()
    {
        await AddRite(Day, None);
        models.Otherwise(Luna, Answer("A build that only passes on its second run."));
        var backfill = Backfill();

        await backfill.StartAsync(CancellationToken.None);
        await (backfill.ExecuteTask ?? Task.CompletedTask);
        var again = Ok(await backfill.Run(CancellationToken.None));

        Assert.True(Ok(await Rites.GetByNumbers([1], CancellationToken.None))[0].Writing.Ailment.IsSome);
        Assert.Equal(0, again);
    }

    [Fact]
    public async Task Run_WhenCosmosFails_ReturnsTheError()
    {
        var missing = new AilmentRepository(cosmos.Container.Database.GetContainer("no-such-container"));
        var backfill = new AilmentBackfill(missing, Writer(), pages, NullLogger<AilmentBackfill>.Instance);

        Assert.True((await backfill.Run(CancellationToken.None)).IsLeft);
    }

    private RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private DraftRepository Drafts => new(cosmos.Container);

    private AilmentRepository Ailments => new(cosmos.Container);

    private AilmentBackfill Backfill() => new(Ailments, Writer(), pages, NullLogger<AilmentBackfill>.Instance);

    private AilmentWriter Writer() => new(models, Microsoft.Extensions.Options.Options.Create(Settings.AilmentWriting), time, probe.Metrics);

    private static string Answer(string ailment) => System.Text.Json.JsonSerializer.Serialize(new { ailment });

    private async Task AddRite(DateOnly day, LanguageExt.Option<string> ailment)
        => Ok(await Rites.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = Words(RiteKind.Ritual, ailment),
        }, CancellationToken.None));

    private async Task<Draft> AddDraft(DraftState state, LanguageExt.Option<string> ailment)
        => Ok(await Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = state,
            Writing = Words(RiteKind.Prayer, ailment),
            PublishedOnUtc = state == DraftState.Published ? Some(Day.AddDays(-1)) : None,
        }, CancellationToken.None));

    private static RiteWriting Words(RiteKind kind, LanguageExt.Option<string> ailment) => new()
    {
        Kind = kind,
        Title = "Rite of the Second Run",
        Text = "Press Re-run, and again.",
        HereticalTruth = "The test depends on timing.",
        Ailment = ailment,
    };
}
