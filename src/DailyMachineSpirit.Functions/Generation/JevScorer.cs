using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Generation;

public class JevOptions
{
    public const string SectionName = "Jev";

    public string Endpoint { get; set; } = "https://jevtypesafeai.com/api/v1/decide";

    /// <summary>Pinned, so scores made months apart stay comparable. A new version means scoring every rite again.</summary>
    public string Model { get; set; } = "jev-1.13.0";

    /// <summary>A Key Vault reference in Azure (the vault's <c>JevApiKey</c>); empty turns scoring off.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// The questions Jev answers about each draft. Which software habit it's about, and what it asks its believers to do,
/// make its similarity scores: each answer is a probability for every option, so two rites are close when their
/// probabilities are. How good it is makes its quality, for the Augury.
/// </summary>
public static class JevQuestions
{
    /// <summary>Changes whenever a topic or act option changes: older scores then aren't comparable any more.</summary>
    public const string QuestionSet = "q1";

    /// <summary>Like <see cref="QuestionSet"/>, for the quality question alone, so changing it keeps the similarity scores.</summary>
    public const string QualityQuestion = "quality-q1";

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Topics =
    [
        new("builds", "builds, compilers, CI pipelines, failing or flaky checks"),
        new("dependencies", "packages, dependencies, versions, lock files, upgrades"),
        new("caches", "caches, temporary files, clean installs, leftover state"),
        new("restarts", "restarting or reinstalling apps, services, machines, routers"),
        new("ai", "AI assistants, prompts, generated code, vibe coding"),
        new("testing", "tests, test coverage, mocks"),
        new("deployment", "deployments, releases, production, rollbacks"),
        new("debugging", "debugging, logs, errors, stack traces"),
        new("configuration", "configuration, environment variables, settings, feature flags"),
        new("infrastructure", "cloud, servers, containers, networks, DNS"),
        new("devices", "hardware, devices, printers, cables, batteries"),
        new("version_control", "version control, branches, merges, commits"),
        new("data", "databases, queries, migrations, backups"),
        new("security", "passwords, certificates, permissions, security"),
        new("performance", "performance, timeouts, waiting, retries"),
        new("other", "none of the above"),
    ];

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Acts =
    [
        new("repetition", "repeating the same action until it works"),
        new("purification", "deleting, cleaning or resetting something"),
        new("incantation", "saying, typing or copying words without understanding them"),
        new("offering", "giving up time, resources or comfort to appease the machine"),
        new("waiting", "waiting, sleeping or adding delays"),
        new("appeasement", "flattering, thanking or pleading with the machine or an AI"),
        new("other", "none of the above"),
    ];

    /// <summary>Each option and the quality it stands for: a draft's quality is their average, weighted by Jev's probabilities.</summary>
    public static readonly IReadOnlyList<(string Key, string Description, float Quality)> Qualities =
    [
        ("excellent", "very funny, a fresh subject, and an accurate, useful explanation", 1f),
        ("good", "funny and accurate, though not surprising", 2f / 3),
        ("fair", "mildly amusing, or the explanation is vague", 1f / 3),
        ("poor", "not funny, confusing, or the explanation is wrong", 0f),
    ];

    public static string Version(string model) => $"{model}/{QuestionSet}";

    public static string QualityVersion(string model) => $"{model}/{QualityQuestion}";
}

/// <summary>
/// Scores drafts with Jev's decide API (jevtypesafeai.com), for "More rites" and the Augury, in one request: only the
/// draft's own text goes there, never anything about visitors. The key is a header, never logged.
/// </summary>
public sealed class JevScorer
{
    private readonly HttpClient http;
    private readonly IOptions<JevOptions> options;
    private readonly TimeProvider time;

    public JevScorer(HttpClient http, IOptions<JevOptions> options, TimeProvider time)
    {
        this.http = http;
        this.options = options;
        this.time = time;
    }

    /// <summary>
    /// The draft with its similarity scores (the topic probabilities, then the act probabilities) and its quality; the
    /// draft as it was while Jev is off.
    /// </summary>
    public async Task<Either<Error, Draft>> Score(Draft draft, CancellationToken cancellationToken)
    {
        var jev = options.Value;
        if (!jev.Enabled)
            return draft;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, jev.Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model = jev.Model,
                    state = $"{draft.Title}\n\n{draft.Text}\n\nWhat really happens: {draft.HereticalTruth}",
                    questions = new Dictionary<string, object>
                    {
                        ["topic"] = Question("Which software habit or practice is this satirical rite about?", JevQuestions.Topics),
                        ["act"] = Question("What does the rite ask its believers to do?", JevQuestions.Acts),
                        ["quality"] = Question(
                            "How good is this as satire of software habits? Judge whether it's funny, and whether its 'What really happens' part is accurate and useful.",
                            JevQuestions.Qualities.Select(q => new KeyValuePair<string, string>(q.Key, q.Description)).ToList()),
                    },
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jev.ApiKey.Trim());

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                // 402 means the credits ran out; the body says why.
                return Error.New($"Jev answered {(int)response.StatusCode}: {Shorten(body)}");

            var answers = JsonNode.Parse(body)?["answers"];
            var topic = answers?["topic"]?["probabilities"];
            var act = answers?["act"]?["probabilities"];
            var quality = answers?["quality"]?["probabilities"];
            if (topic is null || act is null || quality is null)
                return Error.New($"Jev's answer has no probabilities: {Shorten(body)}");

            var now = time.GetUtcNow().UtcDateTime;
            return draft with
            {
                Similarity = new RiteSimilarity
                {
                    ScoresGeneratorVersion = JevQuestions.Version(jev.Model),
                    Scores = [.. Probabilities(topic, JevQuestions.Topics), .. Probabilities(act, JevQuestions.Acts)],
                    CreatedAtUtc = now,
                },
                Augury = new Augury
                {
                    Quality = JevQuestions.Qualities.Sum(q => Probability(quality, q.Key) * q.Quality),
                    JudgedBy = JevQuestions.QualityVersion(jev.Model),
                    JudgedAtUtc = now,
                },
            };
        }
        // An outage, a timeout or an answer that isn't JSON. Only the caller's own cancellation throws.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    private static object Question(string instructions, IReadOnlyList<KeyValuePair<string, string>> options)
        => new { type = "choice", instructions, criteria = options.ToDictionary(o => o.Key, o => o.Value) };

    private static float[] Probabilities(JsonNode probabilities, IReadOnlyList<KeyValuePair<string, string>> options)
        => options.Select(o => Probability(probabilities, o.Key)).ToArray();

    // An option Jev leaves out has probability 0, so every rite's scores line up option by option.
    private static float Probability(JsonNode probabilities, string option) => probabilities[option]?.GetValue<float>() ?? 0f;

    private static string Shorten(string body) => body.Length <= 500 ? body : $"{body[..500]}…";
}
