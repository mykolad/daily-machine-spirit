using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Scoring;

/// <summary>
/// Scores rites for "More rites" with Jev's decide API (jevtypesafeai.com): only the rite's own text goes there, never
/// anything about visitors. The key is a header, never logged.
/// </summary>
public sealed class JevScorer : IRiteScorer
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

    /// <summary>The rite's scores: the topic probabilities, then the act probabilities. None while Jev is off.</summary>
    public async Task<Either<Error, Option<RiteSimilarity>>> Score(Rite rite, CancellationToken cancellationToken)
    {
        var jev = options.Value;
        if (!jev.Enabled)
            return Option<RiteSimilarity>.None;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, jev.Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model = jev.Model,
                    state = $"{rite.Title}\n\n{rite.Text}\n\nWhat really happens: {rite.HereticalTruth}",
                    questions = new Dictionary<string, object>
                    {
                        ["topic"] = Question("Which software habit or practice is this satirical rite about?", JevQuestions.Topics),
                        ["act"] = Question("What does the rite ask its believers to do?", JevQuestions.Acts),
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
            if (topic is null || act is null)
                return Error.New($"Jev's answer has no probabilities: {Shorten(body)}");

            return Some(new RiteSimilarity
            {
                ScoresGeneratorVersion = JevQuestions.Version(jev.Model),
                Scores = [.. Probabilities(topic, JevQuestions.Topics), .. Probabilities(act, JevQuestions.Acts)],
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            });
        }
        // An outage, a timeout or an answer that isn't JSON. Only the caller's own cancellation throws.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    private static object Question(string instructions, IReadOnlyList<KeyValuePair<string, string>> options)
        => new { type = "choice", instructions, criteria = options.ToDictionary(o => o.Key, o => o.Value) };

    // An option Jev leaves out has probability 0, so every rite's scores line up option by option.
    private static float[] Probabilities(JsonNode probabilities, IReadOnlyList<KeyValuePair<string, string>> options)
        => options.Select(o => probabilities[o.Key]?.GetValue<float>() ?? 0f).ToArray();

    private static string Shorten(string body) => body.Length <= 500 ? body : $"{body[..500]}…";
}
