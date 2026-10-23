using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Pages;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// Writes an ailment for the rites and drafts written before rites had one (#115), each time the app starts. After the
/// first run there's nothing left, and it's one small query. Each is saved only to the words it was written from (their
/// ETag), so an amendment or an erasure meanwhile wins; one that fails is left for the next start.
/// </summary>
public sealed class AilmentBackfill : BackgroundService
{
    private readonly IAilmentRepository ailments;
    private readonly AilmentWriter writer;
    private readonly IPageCache pages;
    private readonly ILogger<AilmentBackfill> logger;

    public AilmentBackfill(IAilmentRepository ailments, AilmentWriter writer, IPageCache pages, ILogger<AilmentBackfill> logger)
    {
        this.ailments = ailments;
        this.writer = writer;
        this.pages = pages;
        this.logger = logger;
    }

    /// <summary>How many rites and drafts got their ailment.</summary>
    public async Task<Either<Error, int>> Run(CancellationToken cancellationToken)
        => await (await ailments.GetRitesAndUnpublishedDraftsWithoutAilment(cancellationToken)).MapAsync(async without =>
        {
            var written = 0;
            var published = false;
            foreach (var words in without)
            {
                var saved = await writer.Write(words, cancellationToken)
                    .BindAsync(ailment => ailments.SaveIfUnchangedSinceRead(words, ailment, cancellationToken));
                saved.Match(
                    Right: _ =>
                    {
                        written++;
                        published |= words.Published;
                    },
                    Left: error => logger.LogWarning(error.ToException(), "No ailment was written for {Kind} {Id}; the next start tries again: {Reason}",
                        words.Published ? "rite" : "draft", words.DocumentId, error.Message));
            }
            // The rites API serves the published rites' ailments, kept at the edge until purged.
            if (published)
            {
                await pages.Purge([Paths.RitesApi], cancellationToken);
            }
            return written;
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            (await Run(stoppingToken)).Match(
                Right: written =>
                {
                    if (written > 0)
                    {
                        logger.LogInformation("Wrote the ailment of {Count} rites and drafts written before rites had one.", written);
                    }
                },
                Left: error => logger.LogWarning(error.ToException(), "Older rites' ailments couldn't be written; the next start tries again: {Reason}", error.Message));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping: the next start tries again.
        }
    }
}
