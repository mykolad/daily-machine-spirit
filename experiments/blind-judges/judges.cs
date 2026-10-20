#:package Azure.AI.OpenAI@2.9.0-beta.1
#:package Azure.Identity@1.21.0
#:package Microsoft.Azure.Cosmos@3.63.2
#:package Microsoft.Extensions.AI@10.5.0
#:package Microsoft.Extensions.AI.OpenAI@10.5.0
#:package Newtonsoft.Json@13.0.4
#:property PublishAot=false

// Blind test (#10): who should judge drafts for the Augury? Jev's quality, as now; the other model with a rubric; and
// the other model given the rubric, the Precepts and the Scribes' recent judgments. A one-off, outside the app.
//
//   dotnet run experiments/blind-judges/judges.cs -- score   staging's judged drafts, scored by both model judges
//   dotnet run experiments/blind-judges/judges.cs -- pairs   the pairs where the judges disagree, for the blind page
//
// Reads staging's database alone (as your az login), never production's, and asks staging's models.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;

const string CosmosEndpoint = "https://machinespirit-cosmos.documents.azure.com:443/";
const string StagingDatabase = "machinespirit-staging";
const string ModelsEndpoint = "https://machinespirit-ai.cognitiveservices.azure.com/";
const string Sol = "gpt-6-sol";
const string Luna = "gpt-6-luna";

var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath("experiments/blind-judges/judges.cs"))!, "results");
Directory.CreateDirectory(folder);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var step = args.FirstOrDefault() ?? "score";

if (step == "score")
{
    await Score();
}
else if (step == "pairs")
{
    Pairs();
}
else if (step == "tally")
{
    Tally();
}
else
{
    Console.Error.WriteLine("Steps: score, pairs.");
}

async Task Score()
{
    var credential = new DefaultAzureCredential();
    using var cosmos = new CosmosClient(CosmosEndpoint, credential, new CosmosClientOptions
    {
        UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web),
    });
    var container = cosmos.GetDatabase(StagingDatabase).GetContainer("rites");

    var drafts = await Query<StoredDraft>(container,
        "SELECT c.id, c.state, c.kind, c.title, c.text, c.hereticalTruth, c.generatedByModel, c.augury.quality AS quality " +
        "FROM c WHERE c.type = 'draft' AND IS_DEFINED(c.augury.quality)");
    var precepts = await Query<string>(container, "SELECT VALUE p.text FROM c JOIN p IN c.precepts WHERE c.type = 'precepts'");
    var decisions = await Query<StoredDecision>(container,
        "SELECT TOP 40 c.draftId, c.action, c.title, c.note FROM c WHERE c.type = 'decision' ORDER BY c.decidedAtUtc DESC");
    Console.WriteLine($"{drafts.Count} judged drafts, {precepts.Count} precepts, {decisions.Count} recent decisions.");

    var models = new AzureOpenAIClient(new Uri(ModelsEndpoint), credential);
    using var oneAtATime = new SemaphoreSlim(1);
    var scored = await Task.WhenAll(drafts.Select(async draft =>
    {
        await oneAtATime.WaitAsync();
        try
        {
            // The other model judges, so neither judges its own writing.
            var judge = draft.GeneratedByModel == Sol ? Luna : Sol;
            var chat = models.GetChatClient(judge).AsIChatClient();
            var rubric = await Ask(chat, Rubric, draft);
            // Never the Scribes' judgments of this very draft: that would tell the judge the answer.
            var examples = decisions.Where(decision => decision.DraftId != draft.Id).Take(20).ToList();
            var taught = await Ask(chat, Rubric + Taught(precepts, examples), draft);
            Console.WriteLine($"{draft.Title}: Jev {draft.Quality:F2}, rubric {rubric.Score}, taught {taught.Score}");
            return new ScoredDraft(draft, judge, rubric, taught);
        }
        finally
        {
            oneAtATime.Release();
        }
    }));
    await File.WriteAllTextAsync(Path.Combine(folder, "scores.json"), JsonSerializer.Serialize(scored, json));
    Console.WriteLine($"Wrote {Path.Combine(folder, "scores.json")}.");
}

// For each two judges, the pairs of drafts they order most differently: the pairs that can tell them apart. A draft
// shows in at most four pairs, so no single rite decides the tally. Which side is which is shuffled, and only
// results/pairs.json says which judge preferred which: the page gets the rites alone.
void Pairs()
{
    // A draft a judge gave no valid score is left out: its place in that judge's order is unknown.
    var drafts = JsonSerializer.Deserialize<List<ScoredDraft>>(File.ReadAllText(Path.Combine(folder, "scores.json")), json)!
        .Where(draft => draft.Rubric.Score is >= 1 and <= 10 && draft.Taught.Score is >= 1 and <= 10)
        .ToList();
    (string Name, Func<ScoredDraft, double> Score)[] judges =
    [
        ("jev", draft => draft.Draft.Quality),
        ("rubric", draft => draft.Rubric.Score),
        ("taught", draft => draft.Taught.Score),
    ];
    var random = new Random(10);
    var uses = drafts.ToDictionary(draft => draft.Draft.Id, _ => 0);
    var chosen = new List<(ScoredDraft A, ScoredDraft B)>();
    var candidates = drafts.SelectMany((a, i) => drafts.Skip(i + 1).Select(b => (A: a, B: b))).ToList();
    foreach (var (first, second) in new[] { (0, 1), (0, 2), (1, 2) })
    {
        // How strongly two judges disagree: each one's difference, made comparable by its own spread, multiplied.
        double Spread(int judge) => drafts.Max(judges[judge].Score) - drafts.Min(judges[judge].Score);
        double Disagreement((ScoredDraft A, ScoredDraft B) pair)
        {
            var one = (judges[first].Score(pair.A) - judges[first].Score(pair.B)) / Spread(first);
            var other = (judges[second].Score(pair.A) - judges[second].Score(pair.B)) / Spread(second);
            return one * other < 0 ? -one * other : 0;
        }
        var taken = 0;
        foreach (var pair in candidates.Where(pair => Disagreement(pair) > 0).OrderByDescending(Disagreement))
        {
            if (taken == PairsPerTwoJudges)
            {
                break;
            }
            if (uses[pair.A.Draft.Id] >= 4 || uses[pair.B.Draft.Id] >= 4 || chosen.Contains(pair))
            {
                continue;
            }
            chosen.Add(pair);
            taken++;
            uses[pair.A.Draft.Id]++;
            uses[pair.B.Draft.Id]++;
        }
    }

    var pairs = chosen.OrderBy(_ => random.Next()).Select((pair, index) =>
    {
        var (left, right) = random.Next(2) == 0 ? (pair.A, pair.B) : (pair.B, pair.A);
        var prefers = judges.ToDictionary(judge => judge.Name, judge => Math.Sign(judge.Score(left) - judge.Score(right)) switch
        {
            1 => "left",
            -1 => "right",
            _ => "same",
        });
        return new BlindPair($"pair-{index + 1:D2}", left.Draft, right.Draft, prefers);
    }).ToList();
    File.WriteAllText(Path.Combine(folder, "pairs.json"), JsonSerializer.Serialize(pairs, json));
    File.WriteAllText(Path.Combine(folder, "page-pairs.json"), JsonSerializer.Serialize(
        pairs.Select(pair => new { pair.Id, Left = Shown(pair.Left), Right = Shown(pair.Right) }), json));
    Console.WriteLine($"{pairs.Count} pairs, from {uses.Count(use => use.Value > 0)} drafts.");
}

// For each judge, how often it preferred the rite the owner picked, of the pairs where both had a preference. The
// picks come from the page's database (results/picks-raw/picks/<pair>.json, saved by ArtifactData).
void Tally()
{
    var pairs = JsonSerializer.Deserialize<List<BlindPair>>(File.ReadAllText(Path.Combine(folder, "pairs.json")), json)!;
    var picks = Directory.GetFiles(Path.Combine(folder, "picks-raw", "picks"), "*.json")
        .ToDictionary(file => Path.GetFileNameWithoutExtension(file), file => JsonSerializer.Deserialize<Pick>(File.ReadAllText(file), json)!.Value);
    var decided = pairs.Where(pair => picks.TryGetValue(pair.Id, out var pick) && pick != "same").ToList();
    Console.WriteLine($"{picks.Count} picks: {decided.Count} with a better rite, {picks.Count(pick => pick.Value == "same")} about the same.");
    foreach (var judge in new[] { "jev", "rubric", "taught" })
    {
        var opinions = decided.Where(pair => pair.Prefers[judge] != "same").ToList();
        var agreed = opinions.Count(pair => pair.Prefers[judge] == picks[pair.Id]);
        Console.WriteLine($"{judge,-7} agreed {agreed} of {opinions.Count} ({(opinions.Count == 0 ? 0 : 100.0 * agreed / opinions.Count):F0}%), no preference in {decided.Count - opinions.Count}");
    }
    // Head to head: where two judges preferred opposite rites, which one the owner sided with.
    foreach (var (one, other) in new[] { ("jev", "rubric"), ("jev", "taught"), ("rubric", "taught") })
    {
        var split = decided.Where(pair => pair.Prefers[one] != "same" && pair.Prefers[other] != "same" && pair.Prefers[one] != pair.Prefers[other]).ToList();
        Console.WriteLine($"{one} against {other}: {split.Count(pair => pair.Prefers[one] == picks[pair.Id])} to {split.Count(pair => pair.Prefers[other] == picks[pair.Id])}");
    }
}

static object Shown(StoredDraft draft) => new { draft.Kind, draft.Title, draft.Text, draft.HereticalTruth };

// The deployments' quota is small: past it, wait and ask again.
static async Task<Judgment> Ask(IChatClient chat, string instructions, StoredDraft draft)
{
    for (var tried = 1; ; tried++)
    {
        try
        {
            // An answer outside the scale (an empty one reads as 0) is no verdict: ask again.
            var judgment = await AskOnce(chat, instructions, draft);
            if (judgment.Score is >= 1 and <= 10 || tried >= 3)
            {
                return judgment;
            }
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Status == 429 && tried < 8)
        {
            await Task.Delay(TimeSpan.FromSeconds(15 * tried));
        }
    }
}

static async Task<Judgment> AskOnce(IChatClient chat, string instructions, StoredDraft draft)
{
    var response = await chat.GetResponseAsync<Judgment>(
    [
        new ChatMessage(ChatRole.System, instructions),
        new ChatMessage(ChatRole.User,
            $"A {draft.Kind}.\nTitle: {draft.Title}\nText: {draft.Text}\nHeretical Truth: {draft.HereticalTruth}"),
    ]);
    return response.Result;
}

static async Task<List<T>> Query<T>(Container container, string query)
{
    var found = new List<T>();
    using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("rites") });
    while (iterator.HasMoreResults)
    {
        found.AddRange(await iterator.ReadNextAsync());
    }
    return found;
}

static string Taught(IReadOnlyList<string> precepts, IReadOnlyList<StoredDecision> decisions)
    => "\n\nThe Scribes, the site's moderators, choose what's published. Judge as they would.\n" +
       "Their Precepts, learned from their judgments:\n" + string.Join("\n", precepts.Select(precept => $"- {precept}")) +
       "\n\nTheir recent judgments (anoint: publish next; exalt or humble: move up or down; burn: reject):\n" +
       string.Join("\n", decisions.Select(decision =>
           $"- {decision.Action} \"{decision.Title}\"{(string.IsNullOrWhiteSpace(decision.Note) ? "" : $": \"{decision.Note}\"")}"));

partial class Program
{
    const int PairsPerTwoJudges = 10;

    const string Rubric = """
        You judge drafts for The Daily Machine Spirit, a satirical website. In a far future that forgot the AI, software
        engineers became tech-priests who recite rites to the Machine Spirit instead of understanding their tools. Each day
        it publishes one rite: a prayer or a ritual in solemn liturgical language, with a Heretical Truth, one or two plain
        sentences on what really happens and what to do instead.

        Score how good this rite is to publish, from 1 (flat, wrong or pointless) to 10 (excellent):
        - Funny: dry satire of a habit any developer knows, in the voice of a tech-priest who doesn't understand it.
        - The Heretical Truth is accurate and useful: it says plainly what really happens, and what to do.
        - Original: a sharp observation, not a stock joke, and not one tool's commands and flags.
        - Its rules: it never names AI, models, prompts or chatbots; no Games Workshop names or text; family-friendly.

        Answer with the score and one sentence on why.
        """;
}

record StoredDraft(
    string Id, string State, string Kind, string Title, string Text, string HereticalTruth, string GeneratedByModel, double Quality);

record StoredDecision(string DraftId, string Action, string Title, string? Note);

record Judgment(int Score, string Reason);

record ScoredDraft(StoredDraft Draft, string Judge, Judgment Rubric, Judgment Taught);

record Pick(string Value)
{
    [JsonPropertyName("pick")]
    public string Value { get; init; } = Value;
}

record BlindPair(string Id, StoredDraft Left, StoredDraft Right, Dictionary<string, string> Prefers);
