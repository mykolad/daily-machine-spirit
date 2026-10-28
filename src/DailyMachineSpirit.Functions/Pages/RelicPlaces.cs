using DailyMachineSpirit.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Where each rite the Reliquary keeps stands, read for the relic badges of the pages that list rites.</summary>
public static class RelicPlaces
{
    /// <summary>
    /// Every rite's counts, a few hundred numbers, ranked. A nicety, like More rites: if the counts can't be read, the
    /// page goes without badges rather than failing.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, int>> Read(IRiteReader rites, ILogger logger, CancellationToken cancellationToken)
        => (await rites.GetVotes(cancellationToken)).Match(
            Right: Veneration.Places,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "A page is shown without its relic badges: {Reason}", error.Message);
                return Veneration.NoPlaces;
            });
}
