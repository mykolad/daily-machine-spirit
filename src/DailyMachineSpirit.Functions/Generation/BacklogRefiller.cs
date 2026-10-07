using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>Keeps <see cref="GenerationOptions.BacklogSize"/> drafts waiting, so moderators always have a choice.</summary>
public sealed class BacklogRefiller
{
    private readonly IDraftRepository drafts;
    private readonly IRiteRepository rites;
    private readonly RiteWriter writer;
    private readonly JevScorer scorer;
    private readonly IOptions<GenerationOptions> options;
    private readonly ILogger<BacklogRefiller> logger;

    public BacklogRefiller(
        IDraftRepository drafts,
        IRiteRepository rites,
        RiteWriter writer,
        JevScorer scorer,
        IOptions<GenerationOptions> options,
        ILogger<BacklogRefiller> logger)
    {
        this.drafts = drafts;
        this.rites = rites;
        this.writer = writer;
        this.scorer = scorer;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>
    /// Writes drafts until the backlog is full; returns how many it wrote. It counts again before each one, so two refills
    /// at once (a moderator's and the daily one) overshoot by a draft at most.
    /// </summary>
    public async Task<Either<Error, int>> Refill(CancellationToken cancellationToken)
    {
        for (var written = 0; ; written++)
        {
            var wroteOne = await drafts.GetWaiting(cancellationToken)
                .BindAsync(async waiting => waiting.Count >= options.Value.BacklogSize
                    ? Right<Error, bool>(false)
                    : (await AddDraft(waiting, cancellationToken)).Map(_ => true));
            if (wroteOne.IsLeft)
                return wroteOne.Map(_ => written);
            if (!wroteOne.IfLeft(false))
                return written;
        }
    }

    /// <summary>
    /// Writes, scores and saves one draft, of the kind the backlog has fewer of, on a subject neither the recent rites nor
    /// the <paramref name="waiting"/> drafts have.
    /// </summary>
    public Task<Either<Error, Draft>> AddDraft(IReadOnlyList<Draft> waiting, CancellationToken cancellationToken)
        => rites.GetNewest(options.Value.RecentRitesInPrompt, cancellationToken)
            .BindAsync(recent => writer.Write(
                KindToWrite(waiting),
                [.. recent.Select(rite => rite.Title), .. waiting.Select(draft => draft.Title)],
                cancellationToken))
            .BindAsync(async draft => Right<Error, Draft>(await Scored(draft, cancellationToken)))
            .BindAsync(draft => drafts.Add(draft, cancellationToken));

    private static RiteKind KindToWrite(IReadOnlyList<Draft> waiting)
        => waiting.Count(draft => draft.Kind == RiteKind.Prayer) <= waiting.Count(draft => draft.Kind == RiteKind.Ritual)
            ? RiteKind.Prayer
            : RiteKind.Ritual;

    // Scores only order the backlog and power "More rites": a draft Jev couldn't score still joins it, after the scored
    // ones, rather than being lost.
    private async Task<Draft> Scored(Draft draft, CancellationToken cancellationToken)
        => (await scorer.Score(draft, cancellationToken)).IfLeft(error =>
        {
            logger.LogWarning(error.ToException(), "Draft {Title} joins the backlog without scores: {Reason}", draft.Title, error.Message);
            return draft;
        });
}
