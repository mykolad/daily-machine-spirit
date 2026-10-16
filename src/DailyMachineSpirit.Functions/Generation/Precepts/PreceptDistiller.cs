using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Chat;
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
    /// <summary>How many of the newest judgments a distillation reads: a few months of a weekly visit's.</summary>
    public const int JudgmentsRead = 100;

    /// <summary>The Scribes haven't judged anything yet: there's nothing to learn from.</summary>
    public static readonly Error NothingToLearn = Error.New("The Scribes haven't judged any rite yet, so there's nothing to learn from.");

    /// <summary>Every precept is pinned, which leaves no room for a new one.</summary>
    public static readonly Error AllPinned = Error.New("Every precept is pinned, so there's no room for a new one.");

    private static readonly JsonSerializerOptions AnswerSerializerOptions = new(JsonSerializerOptions.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly IScriptoriumRepository scriptorium;
    private readonly IPreceptRepository precepts;
    private readonly IChatClients chatClients;
    private readonly IOptions<GenerationOptions> options;
    private readonly TimeProvider time;
    private readonly ILogger<PreceptDistiller> logger;

    public PreceptDistiller(
        IScriptoriumRepository scriptorium,
        IPreceptRepository precepts,
        IChatClients chatClients,
        IOptions<GenerationOptions> options,
        TimeProvider time,
        ILogger<PreceptDistiller> logger)
    {
        this.scriptorium = scriptorium;
        this.precepts = precepts;
        this.chatClients = chatClients;
        this.options = options;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Distils and saves the Precepts; returns them.</summary>
    /// <param name="actor">Who asked, for the record: the Machine Spirit (weekly) or a Scribe's id.</param>
    public Task<Either<Error, PreceptBook>> Distil(string actor, CancellationToken cancellationToken)
        => scriptorium.GetDecisions(JudgmentsRead, cancellationToken)
            .BindAsync(decisions => precepts.Get(cancellationToken)
                .BindAsync(read => Distilled(read.Book, decisions, cancellationToken)
                    .BindAsync(book => precepts
                        .Save(read, book, Audit(actor, book, decisions.Count), cancellationToken)
                        .MapAsync(_ => Task.FromResult(book)))));

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
        return (await Answer(messages, room, pinned, cancellationToken)).Map(distilled => book with
        {
            Precepts = [.. pinned, .. distilled.Select(text => new Precept { Id = Guid.NewGuid(), Text = text })],
            DistilledAtUtc = time.GetUtcNow().UtcDateTime,
            DistilledFrom = decisions.Count,
        });
    }

    // Each model once, in order: a Scribe waits for the answer, and the weekly run can wait for next week.
    private async Task<Either<Error, List<string>>> Answer(
        List<ChatMessage> messages, int room, IReadOnlyList<Precept> pinned, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        foreach (var model in options.Value.Models)
        {
            var answer = (await Ask(model, messages, cancellationToken)).Bind(distilled => Checked(distilled, room, pinned));
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
        try
        {
            var response = await chatClients.For(model).GetResponseAsync<DistilledPrecepts>(
                messages, AnswerSerializerOptions, options: null, useJsonSchemaResponseFormat: true, cancellationToken);
            return response.TryGetResult(out var distilled)
                ? distilled
                : Error.New("The answer isn't a list of precepts.");
        }
        // An outage, throttling, a timeout or a refusal: the next model is asked. Only the caller's own cancellation stops.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    // Trimmed, without blanks or repeats of a pinned precept, and no more than there's room for. One too long means the
    // model ignored the rules, so its answer isn't trusted.
    private static Either<Error, List<string>> Checked(DistilledPrecepts distilled, int room, IReadOnlyList<Precept> pinned)
    {
        var texts = distilled.Precepts.Select(text => text.Trim())
            .Where(text => text.Length > 0)
            .Where(text => !pinned.Any(precept => string.Equals(precept.Text, text, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (texts.Count == 0)
        {
            return Error.New("The answer has no precepts.");
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
