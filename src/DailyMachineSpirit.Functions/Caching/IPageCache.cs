namespace DailyMachineSpirit.Functions.Caching;

public interface IPageCache
{
    /// <summary>
    /// Not an <c>Either</c>, unlike other calls that can fail: the caller has nothing to undo (its change is saved), and a
    /// page a failed purge leaves behind refreshes within its edge lifetime anyway.
    /// </summary>
    Task Purge(IReadOnlyCollection<string> paths, CancellationToken cancellationToken);
}
