using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.Expurgation;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The backlog end to end, minus the models and Jev: real repositories in the Cosmos DB emulator, fake models, a fake
/// Jev and a fake clock, wired as the app wires them.
/// </summary>
public sealed class BacklogTestbed : IAsyncLifetime
{
    public const string Sol = "gpt-6-sol";
    public const string Luna = "gpt-6-luna";

    /// <summary>
    /// The precept check's own models, so a check never takes another model's answer. Unless a test lines up another
    /// answer, the first finds nothing breaking a rule, as a precept a test saves usually doesn't.
    /// </summary>
    public const string Checker = "precept-checker";

    public const string SecondChecker = "precept-checker-2";

    /// <summary>
    /// The quality judges' own models, so a judgment never takes another model's answer. Unless a test lines up another
    /// answer, each scores 8 of 10.
    /// </summary>
    public const string QualityJudge = "quality-judge";

    public const string SecondQualityJudge = "quality-judge-2";

    public const string EightOfTen = """{"score": 8, "reason": "A sharp, familiar habit with a useful truth."}""";

    /// <summary>The name suggester's own model, so a test that leaves it unanswered never takes another model's answer.</summary>
    public const string Namer = "scribe-namer";

    /// <summary>The Scribe the tests act as, as an Access id would name them.</summary>
    public const string Tester = "tester-0123456789";

    public static readonly DateOnly Today = new(2026, 10, 7);

    // More than any test adds drafts.
    private const int ScoredPlaces = 64;

    private readonly CosmosTestContainer cosmos = new();
    private int draftsScored;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));

    public FakeChatClients Models { get; } = new FakeChatClients()
        .Otherwise(Checker, """{"breaking": []}""")
        .Otherwise(QualityJudge, EightOfTen)
        .Otherwise(SecondQualityJudge, EightOfTen);

    public FakeJev Jev { get; set; } = FakeJev.AnsweringBuildsAndRepetition();

    public int BacklogSize { get; set; } = 3;

    /// <summary>Empty turns judging off, as an empty <c>Jev:ApiKey</c> does.</summary>
    public string JevApiKey { get; set; } = FakeJev.ApiKey;

    public MetricsProbe Probe { get; } = new();

    public RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    public DraftRepository Drafts => new(cosmos.Container);

    public ScriptoriumRepository Scriptorium => new(cosmos.Container);

    public AuditRepository Audit => new(cosmos.Container);

    public PreceptRepository Precepts => new(cosmos.Container);

    public ScribeNameRepository ScribeNames => new(cosmos.Container);

    public ExpurgationRepository Expurgation => new(cosmos.Container);

    public VoteRepository Votes => new(cosmos.Container);

    public static string Answer(string title)
        => FakeChatClients.Answer(title, "Press Re-run thrice, O Machine Spirit.", "The test is flaky: fix its race instead.");

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync()
    {
        Probe.Dispose();
        return cosmos.DisposeAsync();
    }

    public BacklogRefiller Refiller()
    {
        return new BacklogRefiller(
            Drafts,
            Rites,
            Scriptorium,
            Writer(),
            new DuplicateCheck(Models, Options.Create(Settings.DuplicateRiteCheck), Time, Probe.Metrics),
            DraftJudging(),
            Judge(),
            Options.Create(Settings.Backlog with { DraftsWaiting = BacklogSize }),
            Probe.Metrics,
            NullLogger<BacklogRefiller>.Instance);
    }

    public DailyRitePublisher Publisher() => new(Rites, Drafts, Scriptorium, Refiller(), Options.Create(Settings.Augury), Time, Probe.Metrics, NullLogger<DailyRitePublisher>.Instance);

    public Scribes Scribes() => new(Drafts, Rites, Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages),
        Options.Create(Settings.Backlog with { DraftsWaiting = BacklogSize }), Time);

    public TabCounting Tabs() => new(Drafts, Scriptorium, Precepts, Audit, NullLogger<TabCounting>.Instance);

    public DraftJudging DraftJudging() => new(Drafts, Judge(), Time, NullLogger<DraftJudging>.Instance);

    /// <summary>Jev for the scores and tags, and the quality judges' fake models for the quality, as the app wires them.</summary>
    public RiteJudge Judge() => new(Jev.NewJudge(Time, JevApiKey), new RubricJudge(Models, Options.Create(QualityJudging), Time, Probe.Metrics));

    /// <summary>Both quality judges fail this many times each: no draft, amended or not, gets a score.</summary>
    public void QualityJudgesFail(int times)
    {
        Models.Fails(QualityJudge, times);
        Models.Fails(SecondQualityJudge, times);
    }

    public static QualityJudgingOptions QualityJudging => Settings.QualityJudging with { Models = [QualityJudge, SecondQualityJudge] };

    public PreceptKeeping PreceptKeeping() => new(Precepts, PreceptCheck(), Time, NullLogger<PreceptKeeping>.Instance);

    public ScribeNaming ScribeNaming() => new(ScribeNames, Time);

    public NamesShown NamesShown() => new(ScribeNaming(), NullLogger<NamesShown>.Instance);

    public Expurgations Expurgations(IExpurgationRepository copies, IPageCache pages) => new(Rites, copies, pages, Time);

    /// <summary>Every stored document as JSON, to check what's kept anywhere.</summary>
    public async Task<List<string>> AllDocuments()
    {
        var documents = new List<string>();
        using var iterator = cosmos.Container.GetItemQueryIterator<System.Text.Json.JsonElement>("SELECT * FROM c");
        while (iterator.HasMoreResults)
        {
            documents.AddRange((await iterator.ReadNextAsync()).Select(document => document.GetRawText()));
        }
        return documents;
    }

    public NameSuggester NameSuggester()
        => new(Models, Options.Create(Settings.ScribeNameSuggestions with { Model = Namer }), Time, Probe.Metrics, NullLogger<NameSuggester>.Instance);

    public PreceptDistiller Distiller()
        => new(Scriptorium, Precepts, Models, PreceptCheck(), Options.Create(Settings.PreceptDistilling), Time, Probe.Metrics, NullLogger<PreceptDistiller>.Instance);

    public PreceptCheck PreceptCheck() => new(Models, Options.Create(Settings.PreceptCheck with { Models = [Checker, SecondChecker] }), Time, Probe.Metrics);

    public Amendments Amendments()
        => new(Drafts, Scriptorium, Scribes(), Judge(), Writer(), Time, NullLogger<Amendments>.Instance);

    /// <summary>Scribes whose every read fails, as when Cosmos is down: the container doesn't exist.</summary>
    public TabCounting TabsWithoutCosmos()
    {
        var missing = cosmos.Container.Database.GetContainer("missing");
        return new TabCounting(new DraftRepository(missing), new ScriptoriumRepository(missing), new PreceptRepository(missing), new AuditRepository(missing),
            NullLogger<TabCounting>.Instance);
    }

    public Scribes ScribesWithoutCosmos()
    {
        var missing = cosmos.Container.Database.GetContainer("missing");
        return new Scribes(new DraftRepository(missing), new RiteRepository(missing, NullLogger<RiteRepository>.Instance), new ScriptoriumRepository(missing),
            Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), Time);
    }

    /// <summary>A waiting draft, already judged, as if written by an earlier refill.</summary>
    public async Task<Draft> AddDraft(string title, float quality)
        => await AddDraft(title, quality, RiteKind.Prayer);

    public async Task<Draft> AddDraft(string title, float quality, RiteKind kind)
        => await AddDraft(title, quality, kind, None);

    public async Task<Draft> AddDraft(string title, float quality, RiteKind kind, Option<string[]> tags)
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Writing = new RiteWriting
            {
                Kind = kind,
                Title = title,
                Text = "O Machine Spirit, let the cache be warm.",
                HereticalTruth = "A cold cache is just a cache that hasn't been read yet.",
                Ailment = Some("The build is slow every time the cache is cleared."),
                GeneratedByModel = Sol,
                GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            Tags = tags,
            Augury = new Augury { Quality = quality, JudgedBy = RubricJudge.Version },
        };
        // Scored by the current Jev, so it counts as judged; each on a subject of its own (a score on a place no other
        // draft has), so no two test drafts look alike to the Augury.
        var scores = new float[ScoredPlaces];
        scores[draftsScored++ % ScoredPlaces] = 1f;
        draft = draft with
        {
            Similarity = new RiteSimilarity
            {
                ScoresGeneratorVersion = JevQuestions.Version(Settings.Jev.Model), Scores = scores, CreatedAtUtc = draft.Writing.GeneratedAtUtc,
            },
        };
        return Expect.Ok(await Drafts.Add(draft, CancellationToken.None));
    }

    /// <summary>A waiting draft judging couldn't score: no scores, no quality, no tags.</summary>
    public async Task<Draft> AddUnjudgedDraft(string title)
        => Expect.Ok(await Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Prayer,
                Title = title,
                Text = "O Machine Spirit, let the cache be warm.",
                HereticalTruth = "A cold cache is just a cache that hasn't been read yet.",
                Ailment = Some("The build is slow every time the cache is cleared."),
                GeneratedByModel = Sol,
                GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        }, CancellationToken.None));

    public async Task<Rite> AddRite(DateOnly day, string title)
        => Expect.Ok(await Rites.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = title,
                Text = "Clear the cache, and the cache shall clear thee.",
                HereticalTruth = "A stale cache hides a missing invalidation.",
                GeneratedByModel = Sol,
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
        }, CancellationToken.None));

    // The app's patience, without the pause between tries: the fake models answer at once.
    private RiteWriter Writer()
        => new(Models, Precepts, Options.Create(Settings.RiteWriting with { Patience = Settings.RiteWriting.Patience with { PauseBetweenTries = TimeSpan.Zero } }),
            Time, Probe.Metrics, NullLogger<RiteWriter>.Instance);

    public async Task<List<Draft>> Waiting() => Expect.Ok(await Drafts.GetWaiting(CancellationToken.None));

    public async Task<Rite> PublishedOn(DateOnly day)
        => Expect.Ok(await Rites.GetPublishedOn(day, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException($"Expected a rite on {day}."));
}
