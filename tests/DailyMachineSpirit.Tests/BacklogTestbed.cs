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

    /// <summary>The name suggester's own model, so a test that leaves it unanswered never takes another model's answer.</summary>
    public const string Namer = "scribe-namer";

    /// <summary>The Scribe the tests act as, as an Access id would name them.</summary>
    public const string Tester = "tester-0123456789";

    public static readonly DateOnly Today = new(2026, 10, 7);

    private readonly CosmosTestContainer cosmos = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));

    public FakeChatClients Models { get; } = new FakeChatClients().Otherwise(Checker, """{"breaking": []}""");

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
            JudgingAgain(),
            Jev.NewJudge(Time, JevApiKey),
            Options.Create(Settings.Backlog with { DraftsWaiting = BacklogSize }),
            Probe.Metrics,
            NullLogger<BacklogRefiller>.Instance);
    }

    public DailyRitePublisher Publisher() => new(Rites, Drafts, Scriptorium, Refiller(), Options.Create(Settings.Augury), Time, Probe.Metrics, NullLogger<DailyRitePublisher>.Instance);

    public Scribes Scribes() => new(Drafts, Rites, Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Time);

    public JudgingAgain JudgingAgain() => new(Drafts, Jev.NewJudge(Time, JevApiKey), Time, NullLogger<JudgingAgain>.Instance);

    public PreceptKeeping PreceptKeeping() => new(Precepts, PreceptCheck(), Time, NullLogger<PreceptKeeping>.Instance);

    public ScribeNaming ScribeNaming() => new(ScribeNames, Time);

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
        => new(Drafts, Scriptorium, Scribes(), Jev.NewJudge(Time, JevApiKey), Writer(), Time, NullLogger<Amendments>.Instance);

    /// <summary>Scribes whose every read fails, as when Cosmos is down: the container doesn't exist.</summary>
    public Scribes ScribesWithoutCosmos()
    {
        var missing = cosmos.Container.Database.GetContainer("missing");
        return new Scribes(new DraftRepository(missing), new RiteRepository(missing, NullLogger<RiteRepository>.Instance), new ScriptoriumRepository(missing),
            Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Time);
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
                GeneratedByModel = Sol,
                GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            Tags = tags,
            Augury = new Augury { Quality = quality, JudgedBy = "jev-1.13.0/quality-q1" },
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
