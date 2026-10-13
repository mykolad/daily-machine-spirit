using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class TagIndexTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 9, 20, 48, 0, TimeSpan.Zero));

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Numbers_AreTheTaggedRites_NewestFirst()
    {
        await AddAbout(Day.AddDays(-2), "caches");
        await AddAbout(Day.AddDays(-1), "builds");
        await AddAbout(Day, "caches");

        var index = new TagIndex(Repository, time);

        Assert.Equal([3, 1], Ok(await index.Numbers(Tag("caches"), CancellationToken.None)));
        Assert.Equal([2], Ok(await index.Numbers(Tag("builds"), CancellationToken.None)));
        Assert.Empty(Ok(await index.Numbers(Tag("security"), CancellationToken.None)));
    }

    // Read once per lifetime, however many readers ask: a rite published meanwhile shows when it's read again.
    [Fact]
    public async Task Numbers_KeepTheIndexForItsLifetime_ThenReadItAgain()
    {
        await AddAbout(Day.AddDays(-1), "caches");
        var index = new TagIndex(Repository, time);
        Ok(await index.Numbers(Tag("caches"), CancellationToken.None));

        await AddAbout(Day, "caches");
        var kept = Ok(await index.Numbers(Tag("caches"), CancellationToken.None));
        time.Advance(TagIndex.Lifetime);
        var readAgain = Ok(await index.Numbers(Tag("caches"), CancellationToken.None));

        Assert.Equal([1], kept);
        Assert.Equal([2, 1], readAgain);
    }

    // A popular tag page whose index just expired mustn't send every waiting request to Cosmos.
    [Fact]
    public async Task Numbers_AskedTogether_ShareOneRead()
    {
        await AddAbout(Day, "caches");
        var scores = new GatedScores(Repository);
        var index = new TagIndex(scores, time);

        var asked = Enumerable.Range(0, 5).Select(_ => index.Numbers(Tag("caches"), CancellationToken.None)).ToList();
        scores.Open();
        var answers = await Task.WhenAll(asked);

        Assert.Equal(1, scores.Reads);
        Assert.All(answers, answer => Assert.Equal([1], Ok(answer)));
    }
    [Fact]
    public async Task Numbers_WhenTheScoresCantBeRead_FailAsAnError()
    {
        var index = new TagIndex(new RitesWithoutScores(Repository), time);

        Assert.True((await index.Numbers(Tag("caches"), CancellationToken.None)).IsLeft);
    }

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private static Tag Tag(string key) => RiteTags.Find(key).IfNone(() => throw new ArgumentException(key));

    private async Task AddAbout(DateOnly day, string topic)
    {
        Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting { Kind = RiteKind.Ritual, Title = $"About {topic}", Text = "Clear it.", HereticalTruth = "It was stale." },
        }, CancellationToken.None));
        Ok(await Repository.SaveScores(day, TopicScores.About((topic, 0.9f)), CancellationToken.None));
    }
}
