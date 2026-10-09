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
    // forbids them; an answer that uses one anyway is asked for again. The list catches the setting's distinctive coined
    // names, the ones a model reaches for; it can't be complete, so the prompt's rule stays the first line.
    private static readonly string[] ForbiddenNames =
    [
        "Warhammer", "Games Workshop", "Adeptus", "Mechanicus", "Mechanicum", "Imperium", "Astartes", "Space Marine",
        "Aquila", "Primarch", "Ultramarine", "Horus Heresy", "Emperor of Mankind", "Astra Militarum", "Ecclesiarchy",
        "Tyranid", "Necron", "Eldar", "Aeldari",
    ];

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

    public async Task<Either<Error, Rite>> Write(
        DateOnly publishedOnUtc, RiteKind kind, IReadOnlyList<string> recentTitles, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        List<ChatMessage> messages =
        [
            new(ChatRole.System, RitePrompt.Instructions(kind, recentTitles)),
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
                    .Bind(answer => Check(answer, kind))
                    .Map(draft => new Rite
                    {
                        PublishedOnUtc = publishedOnUtc,
                        Kind = kind,
                        Title = draft.Title,
                        Text = draft.Text,
                        HereticalTruth = draft.HereticalTruth,
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

    private async Task<Either<Error, RiteDraft>> Ask(string model, List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClients.For(model).GetResponseAsync<RiteDraft>(
                messages, AnswerSerializerOptions, options: null, useJsonSchemaResponseFormat: true, cancellationToken);
            return response.TryGetResult(out var draft)
                ? draft
                : Error.New($"The answer isn't a rite: {Shorten(response.Text)}");
        }
        // An outage, throttling, a timeout or a refusal: each is a reason to ask again. Only the caller's own
        // cancellation stops the writing.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    private static Either<Error, RiteDraft> Check(RiteDraft answer, RiteKind kind)
    {
        var draft = answer with
        {
            Title = answer.Title.Trim(),
            Text = answer.Text.Trim(),
            HereticalTruth = answer.HereticalTruth.Trim(),
        };
        if (draft.Title.Length == 0 || draft.Text.Length == 0 || draft.HereticalTruth.Length == 0)
            return Error.New("The answer has an empty field.");
        if (draft.Title.Length > Rite.MaxTitleLength
            || draft.Text.Length > Rite.MaxTextLength
            || draft.HereticalTruth.Length > Rite.MaxHereticalTruthLength)
            return Error.New(
                $"The answer is too long: title {draft.Title.Length}, text {draft.Text.Length}, heretical truth {draft.HereticalTruth.Length} characters.");

        var everything = $"{draft.Title}\n{draft.Text}\n{draft.HereticalTruth}";
        var forbidden = ForbiddenNames.Where(name => everything.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (forbidden.Count > 0)
            return Error.New($"The answer uses forbidden names: {string.Join(", ", forbidden)}.");
        // A prayer may call on the Omnissiah once; a heading, a Heretical Truth or a ritual never names it.
        var allowedInText = kind == RiteKind.Prayer ? 1 : 0;
        if (Occurrences(draft.Title, Omnissiah) + Occurrences(draft.HereticalTruth, Omnissiah) > 0
            || Occurrences(draft.Text, Omnissiah) > allowedInText)
            return Error.New($"The answer names the {Omnissiah} where it isn't allowed: only once, in a prayer's text.");

        // A title is a heading, which shows no code at all: neither backticks nor code written as words.
        if (draft.Title.Contains('`') || UnquotedCode.Find(draft.Title).IsSome)
            return Error.New("The answer's title has code in it.");

        // The pages show `backticked` words as code: a command written as plain words would read as prose.
        return new[] { draft.Text, draft.HereticalTruth }.Select(UnquotedCode.Find).Somes().HeadOrNone()
            .Match<Either<Error, RiteDraft>>(
                Some: code => Error.New($"The answer has code outside backticks: {code}."),
                None: () => draft);
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
