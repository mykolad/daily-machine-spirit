using DailyMachineSpirit.Functions.Caching;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Tests;

public sealed class PurgeBatcherTests : IAsyncLifetime
{
    private readonly FakePageCache pages = new();
    private readonly FakeTimeProvider time = new();
    private readonly PurgeBatcher batcher;

    public PurgeBatcherTests()
    {
        batcher = new PurgeBatcher(pages, Options.Create(Settings.PagePurging), time);
    }

    public Task InitializeAsync() => batcher.StartAsync(CancellationToken.None);

    public Task DisposeAsync() => batcher.StopAsync(CancellationToken.None);

    // However many votes come in, a rite's pages are purged once a minute.
    [Fact]
    public async Task TheMinutesPages_ArePurgedTogether_EachOnce()
    {
        batcher.Add(["/", "/r/1"]);
        time.Advance(TimeSpan.FromSeconds(59));
        batcher.Add(["/", "/r/2"]);
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(["/", "/r/1", "/r/2"], (await pages.NextPurge()).Order());
    }

    [Fact]
    public async Task AMinuteWithoutChanges_PurgesNothing()
    {
        time.Advance(Settings.PagePurging.BatchEvery);
        batcher.Add(["/r/3"]);
        time.Advance(Settings.PagePurging.BatchEvery);

        Assert.Equal(["/r/3"], await pages.NextPurge());
        Assert.Single(pages.Purges);
    }

    [Fact]
    public async Task Stopping_PurgesWhatsWaiting()
    {
        batcher.Add(["/r/4"]);

        await batcher.StopAsync(CancellationToken.None);

        Assert.Equal(["/r/4"], Assert.Single(pages.Purges));
    }
}
