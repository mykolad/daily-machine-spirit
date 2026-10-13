namespace DailyMachineSpirit.Functions.Caching;

/// <summary>
/// The cache in front of the pages (Cloudflare's, in production). Pages are kept there until they change, so whatever
/// changes a page says so here.
/// </summary>
public interface IPageCache
{
    /// <summary>
    /// Drops these pages (paths such as <c>/r/12</c>) from the cache; does nothing where there's no cache to purge. It
    /// never fails its caller, who has nothing to undo: the change is saved, and a page a purge missed refreshes within
    /// its edge lifetime (<see cref="Pages.EdgeLifetime"/>). The implementation logs and counts its failures.
    /// </summary>
    Task Purge(IReadOnlyCollection<string> paths, CancellationToken cancellationToken);
}
