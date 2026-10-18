using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Chat;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>
/// Distils the Precepts (#12) from the Scribes' recent judgments: a model reads them, with their notes, and answers a
/// short list of rules. The pinned precepts stay as they are; the rest make way for the new ones.
/// </summary>
public sealed class PreceptDistiller
{
    /// <summary>The Scribes haven't judged anything yet: there's nothing to learn from.</summary>
    public static readonly Error NothingToLearn = Error.New("The Scribes haven't judged any rite yet, so there's nothing to learn from.");

    public static readonly Error NothingNewToLearn = Error.New("No rite was judged since the Precepts were last distilled, so there's nothing new to learn.");

    /// <summary>Every precept is pinned, which leaves no room for a new one.</summary>
    public static readonly Error AllPinned = Error.New("Every precept is pinned, so there's no room for a new one.");

    public static readonly Error NothingWithinTheRules =
        Error.New("Every precept the Machine Spirit distilled would have the writer break its rules, so the Precepts stay as they were.");

    public static readonly Error Unchecked =
        Error.New("The distilled precepts couldn't be checked against the writer's rules just now, so the Precepts stay as they were.");

    private static readonly JsonSerializerOptions AnswerSerializerOptions = new(JsonSerializerOptions.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly IScriptoriumRepository scriptorium;
    private readonly IPreceptRepository precepts;
    private readonly IChatClients chatClients;
    private readonly PreceptCheck check;
    private readonly IOptions<PreceptDistillingOptions> options;
    private readonly TimeProvider time;
    private readonly SiteMetrics metrics;
    private readonly ILogger<PreceptDistiller> logger;

    public PreceptDistiller(
        IScriptoriumRepository scriptorium,
        IPreceptRepository precepts,
        IChatClients chatClients,
        PreceptCheck check,
        IOptions<PreceptDistillingOptions> options,
        TimeProvider time,
        SiteMetrics metrics,
        ILogger<PreceptDistiller> logger)
    {
        this.scriptorium = scriptorium;
        this.precepts = precepts;
        this.chatClients = chatClients;
        this.check = check;
        this.options = options;
        this.time = time;
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>Distils and saves the Precepts, as a Scribe asked; returns them.</summary>
    public Task<Either<Error, PreceptBook>> Distil(string scribe, CancellationToken cancellationToken)
        => Distil(scribe, onlyWhenJudgedSince: false, cancellationToken);

    /// <summary>
    /// The weekly run's, as the Machine Spirit: only when a rite was judged since the last distillation. The same
    /// judgments would only reword the unpinned precepts, changing the writer's rules for no reason.
    /// </summary>
    public Task<Either<Error, PreceptBook>> DistilIfJudgedSinceLast(CancellationToken cancellationToken)
        => Distil(AuditEntry.MachineSpirit, onlyWhenJudgedSince: true, cancellationToken);

    private Task<Either<Error, PreceptBook>> Distil(string actor, bool onlyWhenJudgedSince, CancellationToken cancellationToken)
        => scriptorium.GetDecisions(options.Value.JudgmentsRead, cancellationToken)
            .BindAsync(decisions => precepts.Get(cancellationToken)
                .BindAsync(read => (onlyWhenJudgedSince && !JudgedSince(read.Book, decisions)
                        ? Task.FromResult(Left<Error, PreceptBook>(NothingNewToLearn))
                        : Distilled(read.Book, decisions, cancellationToken))
                    .BindAsync(book => precepts
                        .Save(read, book, Audit(actor, book, decisions.Count), cancellationToken)
                        .MapAsync(_ => Task.FromResult(book)))));

    // Never distilled counts as new: the first run learns from whatever there is.
    private static bool JudgedSince(PreceptBook book, IReadOnlyList<ScribeDecision> decisions)
        => book.NewestJudgmentReadUtc.ForAll(newestRead => decisions.Any(decision => decision.DecidedAtUtc > newestRead));

    private async Task<Either<Error, PreceptBook>> Distilled(
        PreceptBook book, IReadOnlyList<ScribeDecision> decisions, CancellationToken cancellationToken)
    {
        if (decisions.Count == 0)
        {
            return NothingToLearn;
        }
        var pinned = book.Precepts.Where(precept => precept.Pinned).ToList();
        var room = PreceptBook.MaxPrecepts - pinned.Count;
        if (room <= 0)
        {
            return AllPinned;
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, PreceptPrompt.Instructions(room)),
            new(ChatRole.User, PreceptPrompt.Request(decisions, pinned.Select(precept => precept.Text).ToList(), book.Rejected)),
        };
        // Neither a pinned precept again nor one the Scribes deleted: the page promised it wouldn't come back.
        var excluded = pinned.Select(precept => precept.Text).Concat(book.Rejected).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Unchecked, or nothing left to put in the unpinned ones' place: they stay, and so does what was last read, so the
        // weekly run tries these judgments again.
        return await (await Answer(messages, room, excluded, cancellationToken)).BindAsync(async answered =>
            (await WithinTheRules(answered, cancellationToken)).Bind(distilled => distilled.Count == 0
                ? Left<Error, PreceptBook>(NothingWithinTheRules)
                : book with
                {
                    Precepts = [.. pinned, .. distilled.Select(text => new Precept { Id = Guid.NewGuid(), Text = text })],
                    DistilledAtUtc = time.GetUtcNow().UtcDateTime,
                    DistilledFrom = decisions.Count,
                    NewestJudgmentReadUtc = decisions.Max(decision => decision.DecidedAtUtc),
                }));
    }

    // A distilled precept that would have the writer break its rules is dropped: followed, it would fail drafts on
    // RiteChecks or be ignored. Unchecked, none is kept: the writer would follow them in every rite.
    private async Task<Either<Error, List<string>>> WithinTheRules(List<string> distilled, CancellationToken cancellationToken)
        => (await check.Breaches(distilled, cancellationToken)).Match(
            Right: breaches =>
            {
                foreach (var breach in breaches)
                {
                    logger.LogWarning("A distilled precept was dropped for breaking the rules: {Precept} ({Reason})", breach.Precept, breach.Reason);
                }
                var dropped = breaches.Select(breach => breach.Precept).ToHashSet();
                return Right<Error, List<string>>(distilled.Where(text => !dropped.Contains(text)).ToList());
            },
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The distilled precepts couldn't be checked against the rules: {Reason}", error.Message);
                return Left<Error, List<string>>(Unchecked);
            });

    // Each model in order, its tries each within the patience's time: a Scribe waits for the answer, and the weekly run can
    // wait for next week.
    private async Task<Either<Error, List<string>>> Answer(
        List<ChatMessage> messages, int room, IReadOnlySet<string> excluded, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var patience = options.Value.Patience;
        foreach (var model in options.Value.Models.SelectMany(model => Enumerable.Repeat(model, patience.TriesPerModel)))
        {
            var answer = (await Ask(model, messages, cancellationToken)).Bind(distilled => Checked(distilled, room, excluded));
            if (answer.IsRight)
            {
                return answer;
            }
            answer.IfLeft(error =>
            {
                logger.LogWarning(error.ToException(), "{Model} didn't distil usable precepts: {Reason}", model, error.Message);
                failures.Add($"{model}: {error.Message}");
            });
        }

        return Error.New($"No model distilled usable precepts. {string.Join(" | ", failures)}");
    }

    private async Task<Either<Error, DistilledPrecepts>> Ask(string model, List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        using var timeout = options.Value.Patience.TimerForATry(time);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = time.GetTimestamp();
        try
        {
            var response = await chatClients.For(model).GetResponseAsync<DistilledPrecepts>(
                messages, AnswerSerializerOptions, options: null, useJsonSchemaResponseFormat: true, attempt.Token);
            metrics.AnswerTime(model, "distil", "answered", time.GetElapsedTime(started));
            return response.TryGetResult(out var distilled)
                ? distilled
                : Error.New("The answer isn't a list of precepts.");
        }
        // An outage, throttling, running out of time or a refusal: the next model is asked. Only the caller's own
        // cancellation stops.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            metrics.AnswerTime(model, "distil", timeout.IsCancellationRequested ? "timed-out" : "failed", time.GetElapsedTime(started));
            return Error.New(ex);
        }
    }

    // Trimmed, without blanks or excluded precepts, and no more than there's room for. A null or one too long means the
    // model ignored the rules, so its answer isn't trusted.
    private static Either<Error, List<string>> Checked(DistilledPrecepts distilled, int room, IReadOnlySet<string> excluded)
    {
        // The serializer checks the list isn't null, but not what's in it.
        if (distilled.Precepts.Any(text => text is null))
        {
            return Error.New("The answer has a null precept.");
        }
        var texts = distilled.Precepts.Select(text => text.Trim())
            .Where(text => text.Length > 0 && !excluded.Contains(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (texts.Count == 0)
        {
            return Error.New("The answer has no new precepts.");
        }
        if (texts.Any(text => text.Length > Precept.MaxLength))
        {
            return Error.New($"A precept is longer than {Precept.MaxLength} characters.");
        }
        return texts.Take(room).ToList();
    }

    private AuditEntry Audit(string actor, PreceptBook book, int judgments) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = actor,
        Action = AuditAction.Distil,
        Detail = Some($"{book.Precepts.Count(precept => !precept.Pinned)} new, {book.Precepts.Count(precept => precept.Pinned)} pinned, from {judgments} judgments"),
    };
}
