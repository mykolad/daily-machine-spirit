using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Chat;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// Asks the models for a rite, each up to <see cref="GenerationOptions.AttemptsPerModel"/> times, in
/// <see cref="GenerationOptions.Models"/> order. An unusable answer is asked for again, never cut or patched up: a cut
/// rite would read as broken, and a patched one isn't what the model wrote.
/// </summary>
public sealed class RiteWriter
{
    private const string Omnissiah = "Omnissiah";

    // The satire borrows its mood from a setting whose names it must not use (CLAUDE.md, Ground rules). The prompt
    // forbids them; an answer that uses one anyway is asked for again.
    private static readonly string[] ForbiddenNames =
        ["Warhammer", "Games Workshop", "Adeptus", "Mechanicus", "Imperium", "Astartes", "Space Marine", "Aquila"];

    private static readonly JsonSerializerOptions AnswerSerializerOptions = new(JsonSerializerOptions.Web)
    {
        // A missing or null field makes the answer unusable, rather than a rite with a hole in it.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly IChatClients chatClients;
    private readonly IOptions<GenerationOptions> options;
    private readonly TimeProvider time;
    private readonly ILogger<RiteWriter> logger;

    public RiteWriter(IChatClients chatClients, IOptions<GenerationOptions> options, TimeProvider time, ILogger<RiteWriter> logger)
    {
        this.chatClients = chatClients;
        this.options = options;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>A new draft of the kind, on a subject none of <paramref name="titlesToAvoid"/> has; not scored or saved yet.</summary>
    public async Task<Either<Error, Draft>> Write(RiteKind kind, IReadOnlyList<string> titlesToAvoid, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        List<ChatMessage> messages =
        [
            new(ChatRole.System, RitePrompt.Instructions(kind, titlesToAvoid)),
            new(ChatRole.User, RitePrompt.Request(kind)),
        ];

        var failures = new List<string>();
        foreach (var model in settings.Models)
        {
            for (var attempt = 1; attempt <= settings.AttemptsPerModel; attempt++)
            {
                // A pause before asking again: an overloaded or throttled model often answers a little later.
                if (failures.Count > 0)
                    await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), time, cancellationToken);

                var written = (await Ask(model, messages, cancellationToken))
                    .Bind(Check)
                    .Map(content => new Draft
                    {
                        Id = Guid.NewGuid(),
                        State = DraftState.Waiting,
                        Kind = kind,
                        Title = content.Title,
                        Text = content.Text,
                        HereticalTruth = content.HereticalTruth,
                        GeneratedByModel = model,
                        GeneratedAtUtc = time.GetUtcNow().UtcDateTime,
                    });
                if (written.IsRight)
                {
                    // The model shows whether the fallback had to step in, even when the run succeeds.
                    written.IfRight(rite => logger.LogInformation(
                        "{Model} wrote the {Kind} \"{Title}\" on try {Attempt}, after {FailedTries} failed tries in all.",
                        model, kind, rite.Title, attempt, failures.Count));
                    return written;
                }

                written.IfLeft(error =>
                {
                    logger.LogWarning(error.ToException(), "{Model} didn't write a usable rite (try {Attempt}): {Reason}",
                        model, attempt, error.Message);
                    failures.Add($"{model}, try {attempt}: {error.Message}");
                });
            }
        }

        return Error.New($"No model wrote a usable {kind.ToString().ToLowerInvariant()}. {string.Join(" | ", failures)}");
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

    private static Either<Error, RiteContent> Check(RiteContent answer)
    {
        var content = answer with
        {
            Title = answer.Title.Trim(),
            Text = answer.Text.Trim(),
            HereticalTruth = answer.HereticalTruth.Trim(),
        };
        if (content.Title.Length == 0 || content.Text.Length == 0 || content.HereticalTruth.Length == 0)
            return Error.New("The answer has an empty field.");
        if (content.Title.Length > Rite.MaxTitleLength
            || content.Text.Length > Rite.MaxTextLength
            || content.HereticalTruth.Length > Rite.MaxHereticalTruthLength)
            return Error.New(
                $"The answer is too long: title {content.Title.Length}, text {content.Text.Length}, heretical truth {content.HereticalTruth.Length} characters.");

        var everything = $"{content.Title}\n{content.Text}\n{content.HereticalTruth}";
        var forbidden = ForbiddenNames.Where(name => everything.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (forbidden.Count > 0)
            return Error.New($"The answer uses forbidden names: {string.Join(", ", forbidden)}.");
        if (Occurrences(everything, Omnissiah) > 1)
            return Error.New($"The answer names the {Omnissiah} more than once.");

        return content;
    }

    private static int Occurrences(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(word, at + word.Length, StringComparison.OrdinalIgnoreCase))
            count++;
        return count;
    }

    private static string Shorten(string text) => text.Length <= 500 ? text : $"{text[..500]}…";
}
