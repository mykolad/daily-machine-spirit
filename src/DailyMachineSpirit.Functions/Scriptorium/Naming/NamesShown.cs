using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>
/// The Scribes' names as a page shows them: who's looking, and who did what. A nicety: when they can't be read, it's
/// logged, and the page shows Scribes by their ids.
/// </summary>
public sealed class NamesShown
{
    private readonly ScribeNaming naming;
    private readonly ILogger<NamesShown> logger;

    public NamesShown(ScribeNaming naming, ILogger<NamesShown> logger)
    {
        this.naming = naming;
        this.logger = logger;
    }

    /// <summary>How the Record shows the Scribe looking, and whether they chose it; none when the names can't be read.</summary>
    public async Task<Option<NameInTheRecord>> Of(string scribe, CancellationToken cancellationToken)
        => (await naming.NameOf(scribe, cancellationToken)).Match(
            Right: chosen => Some(new NameInTheRecord(chosen.IfNone(() => ScribeNames.Unnamed(scribe)), chosen.IsSome)),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scribes' names couldn't be read: {Reason}", error.Message);
                return Option<NameInTheRecord>.None;
            });

    /// <summary>Every name the Scribes chose; none when they can't be read.</summary>
    public async Task<List<ScribeName>> All(CancellationToken cancellationToken)
        => (await naming.All(cancellationToken)).Match(
            Right: all => all,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scribes' names couldn't be read: {Reason}", error.Message);
                return [];
            });
}
