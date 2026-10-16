using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Chat;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// Asks the models for a rite, each up to <see cref="GenerationOptions.AttemptsPerModel"/> times, in
/// <see cref="GenerationOptions.Models"/> order. An unusable answer (<see cref="RiteChecks"/>) is asked for again, never
/// cut or patched up: a cut rite would read as broken, and a patched one isn't what the model wrote.
/// </summary>
public sealed class RiteWriter
{
    private static readonly JsonSerializerOptions AnswerSerializerOptions = new(JsonSerializerOptions.Web)
    {
        // A missing or null field makes the answer unusable, rather than a rite with a hole in it.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly IChatClients chatClients;
    private readonly IOptions<GenerationOptions> options;
    private readonly TimeProvider time;
    private readonly SiteMetrics metrics;
    private readonly ILogger<RiteWriter> logger;

    public RiteWriter(
        IChatClients chatClients, IOptions<GenerationOptions> options, TimeProvider time, SiteMetrics metrics, ILogger<RiteWriter> logger)
    {
        this.chatClients = chatClients;
        this.options = options;
        this.time = time;
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>A new draft of the kind, on a subject none of <paramref name="titlesToAvoid"/> has; not judged or saved yet.</summary>
    public async Task<Either<Error, Draft>> Write(RiteKind kind, IReadOnlyList<string> titlesToAvoid, CancellationToken cancellationToken)
        => (await Answer(
                [new(ChatRole.System, RitePrompt.Instructions(kind, titlesToAvoid)), new(ChatRole.User, RitePrompt.Request(kind))],
                kind,
                $"No model wrote a usable {kind.ToString().ToLowerInvariant()}.",
                cancellationToken))
            .Map(written => new Draft
            {
                Id = Guid.NewGuid(),
                State = DraftState.Waiting,
                Writing = new RiteWriting
                {
                    Kind = kind,
                    Title = written.Content.Title,
                    Text = written.Content.Text,
                    HereticalTruth = written.Content.HereticalTruth,
                    GeneratedByModel = written.Model,
                    GeneratedAtUtc = time.GetUtcNow().UtcDateTime,
                },
            });

    /// <summary>
    /// The rite's words rewritten as a Scribe's note asks (#8), kept wherever the note doesn't ask for a change. Not
    /// saved: the Scribe reads the rewrite before amending the draft with it.
    /// </summary>
    public async Task<Either<Error, RiteContent>> Rewrite(RiteKind kind, RiteContent words, string note, CancellationToken cancellationToken)
        => (await Answer(
                [new(ChatRole.System, RitePrompt.Instructions(kind, [])), new(ChatRole.User, RitePrompt.Rewrite(kind, words, note))],
                kind,
                "No model rewrote it usably.",
                cancellationToken))
            .Map(written => written.Content);

    // The first usable answer to the messages, and the model that gave it.
    private async Task<Either<Error, (RiteContent Content, string Model)>> Answer(
        List<ChatMessage> messages, RiteKind kind, string noneUsable, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var failures = new List<string>();
        foreach (var model in settings.Models)
        {
            for (var attempt = 1; attempt <= settings.AttemptsPerModel; attempt++)
            {
                // A pause before asking again: an overloaded or throttled model often answers a little later.
                if (failures.Count > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), time, cancellationToken);
                }

                var answer = await Ask(model, messages, cancellationToken);
                var written = answer.Bind(content => RiteChecks.Check(content, kind));
                metrics.Answer(model, answer.IsLeft ? "failed" : written.IsLeft ? "refused" : "accepted");
                if (written.IsRight)
                {
                    // The model shows whether the fallback had to step in, even when the run succeeds.
                    written.IfRight(content => logger.LogInformation(
                        "{Model} wrote the {Kind} \"{Title}\" on try {Attempt}, after {FailedTries} failed tries in all.",
                        model, kind, content.Title, attempt, failures.Count));
                    return written.Map(content => (content, model));
                }

                written.IfLeft(error =>
                {
                    logger.LogWarning(error.ToException(), "{Model} didn't write a usable rite (try {Attempt}): {Reason}",
                        model, attempt, error.Message);
                    failures.Add($"{model}, try {attempt}: {error.Message}");
                });
            }
        }

        return Error.New($"{noneUsable} {string.Join(" | ", failures)}");
    }

    private async Task<Either<Error, RiteContent>> Ask(string model, List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClients.For(model).GetResponseAsync<RiteContent>(
                messages, AnswerSerializerOptions, options: null, useJsonSchemaResponseFormat: true, cancellationToken);
            return response.TryGetResult(out var content)
                ? content
                : Error.New($"The answer isn't a rite: {Shorten(response.Text)}");
        }
        // An outage, throttling, a timeout or a refusal: each is a reason to ask again. Only the caller's own
        // cancellation stops the writing.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    private static string Shorten(string text) => text.Length <= 500 ? text : $"{text[..500]}…";
}
