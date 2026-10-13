using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Tags;

/// <summary>
/// A rite's tags, read from its similarity scores: Jev's answer to which software habit the rite is about. Every topic
/// Jev gives at least <see cref="MinProbability"/> is a tag, the likeliest first, at most <see cref="MaxPerRite"/>. So
/// tags cost no extra request and need no stored copy (rites judged before tags existed have theirs), and they come
/// from a fixed vocabulary: no "cache" next to "caches". "None of the above" is never a tag, and scores from another
/// judge or question set give none.
/// </summary>
public static class RiteTags
{
    // On staging's rites, a second topic Jev gives .3 to .45 fits the rite; at .2 and below it mostly doesn't.
    public const float MinProbability = 0.25f;

    public const int MaxPerRite = 2;

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["builds"] = "Builds",
        ["dependencies"] = "Dependencies",
        ["caches"] = "Caches",
        ["restarts"] = "Restarts",
        ["ai"] = "AI",
        ["testing"] = "Testing",
        ["deployment"] = "Deployment",
        ["debugging"] = "Debugging",
        ["configuration"] = "Configuration",
        ["infrastructure"] = "Infrastructure",
        ["devices"] = "Devices",
        ["version_control"] = "Version control",
        ["data"] = "Data",
        ["security"] = "Security",
        ["performance"] = "Performance",
    };

    // Each tag with its topic's place in the scores, which start with the topics' probabilities in JevQuestions' order.
    private static readonly List<(Tag Tag, int Score)> Topics = JevQuestions.Topics
        .Select((topic, index) => (topic, index))
        .Where(topic => topic.topic.Key != "other")
        .Select(topic => (new Tag(topic.topic.Key.Replace('_', '-'), Labels[topic.topic.Key], topic.topic.Value), topic.index))
        .ToList();

    public static IReadOnlyList<Tag> All { get; } = Topics.Select(topic => topic.Tag).ToList();

    public static Option<Tag> Find(string key) => All.Where(tag => tag.Key == key).HeadOrNone();

    public static List<Tag> Of(Option<RiteSimilarity> similarity)
        => similarity
            .Filter(scores => JevQuestions.IsVersion(scores.ScoresGeneratorVersion) && scores.Scores.Length >= JevQuestions.Topics.Count)
            .Map(scores => Topics
                .Where(topic => scores.Scores[topic.Score] >= MinProbability)
                .OrderByDescending(topic => scores.Scores[topic.Score])
                .Take(MaxPerRite)
                .Select(topic => topic.Tag)
                .ToList())
            .IfNone([]);
}
