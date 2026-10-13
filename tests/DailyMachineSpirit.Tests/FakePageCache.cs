using DailyMachineSpirit.Functions.Caching;

namespace DailyMachineSpirit.Tests;

/// <summary>A page cache that records each purge's paths.</summary>
public sealed class FakePageCache : IPageCache
{
    public List<IReadOnlyCollection<string>> Purges { get; } = [];

    public Task Purge(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        Purges.Add(paths);
        return Task.CompletedTask;
    }
}
