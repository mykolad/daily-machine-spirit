using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace DailyMachineSpirit.Functions.Caching;

/// <summary>
/// Gathers the pages votes change and purges them together once a minute, so a vote never waits for Cloudflare, and a
/// rite's pages are purged at most once a minute however many votes come in. Other readers see a vote within that
/// minute; the voter sees theirs at once (the page's script). Whatever is still waiting when the app stops is purged
/// then.
/// </summary>
public sealed class PurgeBatcher : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, byte> waiting = new();
    private readonly IPageCache cache;
    // Started here, not in ExecuteAsync, which runs on its own thread whenever it gets there: the first minute starts with
    // the app, so a test that moves the clock knows where it stands.
    private readonly PeriodicTimer timer;

    public PurgeBatcher(IPageCache cache, TimeProvider time)
    {
        this.cache = cache;
        timer = new PeriodicTimer(Interval, time);
    }

    public void Add(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            waiting.TryAdd(path, 0);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await Purge(cancellationToken);
    }

    public override void Dispose()
    {
        timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await Purge(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: StopAsync purges what's left.
        }
    }

    // A path added while this runs either made it into this purge or stays for the next.
    private Task Purge(CancellationToken cancellationToken)
    {
        var paths = waiting.Keys.Where(path => waiting.TryRemove(path, out _)).ToList();
        return paths.Count == 0 ? Task.CompletedTask : cache.Purge(paths, cancellationToken);
    }
}
