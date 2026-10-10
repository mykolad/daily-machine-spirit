using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The backlog end to end, minus the models and Jev: real repositories in the Cosmos DB emulator, fake models, a fake
/// Jev and a fake clock, wired as the app wires them.
/// </summary>
public sealed class BacklogTestbed : IAsyncLifetime
{
    public const string Sol = "gpt-6-sol";
    public const string Luna = "gpt-6-luna";

    public static readonly DateOnly Today = new(2026, 10, 7);

    private readonly CosmosTestContainer cosmos = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));

    public FakeChatClients Models { get; } = new();

    public FakeJev Jev { get; set; } = FakeJev.AnsweringBuildsAndRepetition();

    public int BacklogSize { get; set; } = 3;

    /// <summary>Empty turns scoring off, as an empty <c>Jev:ApiKey</c> does.</summary>
    public string JevApiKey { get; set; } = FakeJev.ApiKey;

    public MetricsProbe Probe { get; } = new();

    public RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    public DraftRepository Drafts => new(cosmos.Container);

    public ScriptoriumRepository Scriptorium => new(cosmos.Container);

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
        var options = Options.Create(new GenerationOptions { RetryDelaySeconds = 0, BacklogSize = BacklogSize });
        return new BacklogRefiller(
            Drafts,
            Rites,
            Scriptorium,
            new RiteWriter(Models, options, Time, Probe.Metrics, NullLogger<RiteWriter>.Instance),
            Jev.Scorer(Time, JevApiKey),
            options,
            Probe.Metrics,
            NullLogger<BacklogRefiller>.Instance);
    }

    public DailyRitePublisher Publisher() => new(Rites, Drafts, Scriptorium, Refiller(), Time, Probe.Metrics);

    public Scribes Scribes() => new(Drafts, Rites, Scriptorium, Time);

    /// <summary>Scribes whose every read fails, as when Cosmos is down: the container doesn't exist.</summary>
    public Scribes ScribesWithoutCosmos()
    {
        var missing = cosmos.Container.Database.GetContainer("missing");
        return new Scribes(new DraftRepository(missing), new RiteRepository(missing, NullLogger<RiteRepository>.Instance), new ScriptoriumRepository(missing), Time);
    }

    /// <summary>A waiting draft, already judged, as if written by an earlier refill.</summary>
    public async Task<Draft> AddDraft(string title, float quality)
        => await AddDraft(title, quality, RiteKind.Prayer);

    public async Task<Draft> AddDraft(string title, float quality, RiteKind kind)
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Kind = kind,
            Title = title,
            Text = "O Machine Spirit, let the cache be warm.",
            HereticalTruth = "A cold cache is just a cache that hasn't been read yet.",
            GeneratedByModel = Sol,
            GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Augury = new Augury { Quality = quality, JudgedBy = "jev-1.13.0/quality-q1" },
        };
        return Expect.Ok(await Drafts.Add(draft, CancellationToken.None));
    }

    public async Task<Rite> AddRite(DateOnly day, string title)
        => Expect.Ok(await Rites.Add(new Rite
        {
            PublishedOnUtc = day,
            Kind = RiteKind.Ritual,
            Title = title,
            Text = "Clear the cache, and the cache shall clear thee.",
            HereticalTruth = "A stale cache hides a missing invalidation.",
            GeneratedByModel = Sol,
            GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        }, CancellationToken.None));

    public async Task<List<Draft>> Waiting() => Expect.Ok(await Drafts.GetWaiting(CancellationToken.None));

    public async Task<Rite> PublishedOn(DateOnly day)
        => Expect.Ok(await Rites.GetPublishedOn(day, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException($"Expected a rite on {day}."));
}
