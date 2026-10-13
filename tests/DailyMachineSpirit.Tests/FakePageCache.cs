using System.Threading.Channels;
using DailyMachineSpirit.Functions.Caching;

namespace DailyMachineSpirit.Tests;

/// <summary>A page cache that records each purge's paths.</summary>
public sealed class FakePageCache : IPageCache
{
    private readonly Channel<IReadOnlyCollection<string>> purged = Channel.CreateUnbounded<IReadOnlyCollection<string>>();

    public List<IReadOnlyCollection<string>> Purges { get; } = [];

    public Task Purge(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        lock (Purges)
        {
            Purges.Add(paths);
        }
        purged.Writer.TryWrite(paths);
        return Task.CompletedTask;
    }

    /// <summary>The next purge not yet waited for, from a background service: at most five seconds away.</summary>
    public async Task<IReadOnlyCollection<string>> NextPurge()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await purged.Reader.ReadAsync(timeout.Token);
    }
}
