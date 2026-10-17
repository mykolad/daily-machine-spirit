using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Telemetry;
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
    private readonly IScriptoriumRepository scriptorium;
    private readonly RiteWriter writer;
    private readonly DuplicateCheck duplicates;
    private readonly IRiteJudge judge;
    private readonly IOptions<GenerationOptions> options;
    private readonly SiteMetrics metrics;
    private readonly ILogger<BacklogRefiller> logger;

    public BacklogRefiller(
        IDraftRepository drafts,
        IRiteRepository rites,
        IScriptoriumRepository scriptorium,
        RiteWriter writer,
        DuplicateCheck duplicates,
        IRiteJudge judge,
        IOptions<GenerationOptions> options,
        SiteMetrics metrics,
        ILogger<BacklogRefiller> logger)
    {
        this.drafts = drafts;
        this.rites = rites;
        this.scriptorium = scriptorium;
        this.writer = writer;
        this.duplicates = duplicates;
        this.judge = judge;
        this.options = options;
        this.metrics = metrics;
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
            {
                return wroteOne.Map(_ => written);
            }

            if (!wroteOne.IfLeft(false))
            {
                return written;
            }
        }
    }

    /// <summary>
    /// Writes, judges and saves one draft, of the kind the backlog has fewer of, repeating no rite already written. The
    /// prompt lists the recent rites, the <paramref name="waiting"/> drafts and the recently burned ones (they teach the
    /// model what the Scribes didn't want), each with its truth; then a model checks the draft against those and every
    /// rite ever published. One that repeats a rite is written again, up to <see cref="GenerationOptions.DuplicateRewrites"/>
    /// times, then kept, marked with what it repeats, for the Scribes to judge.
    /// </summary>
    public Task<Either<Error, Draft>> AddDraft(IReadOnlyList<Draft> waiting, CancellationToken cancellationToken)
        => rites.GetAllWords(cancellationToken)
            .BindAsync(published => scriptorium.GetBurned(options.Value.RecentRitesInPrompt, cancellationToken)
                .BindAsync(burned =>
                {
                    List<TakenRite> drafted = [.. waiting.Select(Taken), .. burned.Select(Taken)];
                    var recent = published.Take(options.Value.RecentRitesInPrompt).Select(Taken);
                    return Unrepeated(
                        KindToWrite(waiting),
                        [.. recent, .. drafted],
                        [.. published.Select(Taken), .. drafted],
                        options.Value.DuplicateRewrites,
                        cancellationToken);
                }))
            .BindAsync(async draft => Right<Error, Draft>(await Judged(draft, cancellationToken)))
            .BindAsync(draft => drafts.Add(draft, cancellationToken));

    // A draft that repeats a rite is written again, told of that rite; once the rewrites run out, it's kept and marked.
    private Task<Either<Error, Draft>> Unrepeated(
        RiteKind kind, IReadOnlyList<TakenRite> listed, IReadOnlyList<TakenRite> taken, int rewritesLeft, CancellationToken cancellationToken)
        => writer.Write(kind, listed, cancellationToken)
            .BindAsync(async draft => await (await Repeated(draft, taken, cancellationToken)).Match(
                Some: same => rewritesLeft > 0
                    ? Unrepeated(kind, [.. listed, same], taken, rewritesLeft - 1, cancellationToken)
                    : Task.FromResult(Right<Error, Draft>(draft with { Resembles = same.Title })),
                None: () => Task.FromResult(Right<Error, Draft>(draft))));

    // Checking only guards the backlog: a draft the model couldn't check is kept rather than lost, as an unjudged one is.
    private async Task<Option<TakenRite>> Repeated(Draft draft, IReadOnlyList<TakenRite> taken, CancellationToken cancellationToken)
        => (await duplicates.Repeats(draft, taken, cancellationToken)).Match(
            Right: same =>
            {
                same.Match(
                    Some: found =>
                    {
                        logger.LogInformation("Draft {Title} repeats {Repeated}.", draft.Writing.Title, found.Title);
                        metrics.DuplicateCheck("repeated");
                    },
                    None: () => metrics.DuplicateCheck("unique"));
                return same;
            },
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "Draft {Title} couldn't be checked for repeats: {Reason}", draft.Writing.Title, error.Message);
                metrics.DuplicateCheck("failed");
                return Option<TakenRite>.None;
            });

    private static TakenRite Taken(RiteWords rite) => new(rite.Title, rite.HereticalTruth);

    private static TakenRite Taken(Draft draft) => new(draft.Writing.Title, draft.Writing.HereticalTruth);

    private static RiteKind KindToWrite(IReadOnlyList<Draft> waiting)
        => waiting.Count(draft => draft.Writing.Kind == RiteKind.Prayer) <= waiting.Count(draft => draft.Writing.Kind == RiteKind.Ritual)
            ? RiteKind.Prayer
            : RiteKind.Ritual;

    // Judging only orders the backlog and powers "More rites": a draft Jev couldn't judge still joins it, after the
    // judged ones, rather than being lost.
    private async Task<Draft> Judged(Draft draft, CancellationToken cancellationToken)
        => (await judge.Judge(draft, cancellationToken)).Match(
            Right: judged =>
            {
                judged.Similarity.Match(
                    Some: scores =>
                    {
                        logger.LogInformation("Draft {Title} was judged by {Judge}, its scores made by {ScoresGenerator}.",
                            draft.Writing.Title, judged.Augury.Map(augury => augury.JudgedBy).IfNone("no quality judge"), scores.ScoresGeneratorVersion);
                        metrics.Judging("judged");
                    },
                    None: () =>
                    {
                        logger.LogInformation("Draft {Title} joins the backlog without scores: judging is turned off.", draft.Writing.Title);
                        metrics.Judging("off");
                    });
                return judged;
            },
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "Draft {Title} joins the backlog without scores: {Reason}", draft.Writing.Title, error.Message);
                metrics.Judging("failed");
                return draft;
            });
}
