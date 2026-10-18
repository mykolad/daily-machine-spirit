using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// A rite's tags from Jev's answer to which software habit it's about: the topics Jev gives at least
/// <see cref="JevOptions.TagMinProbability"/>, the likeliest first, at most <see cref="JevOptions.TagsPerRite"/>. "None of the above" is never a
/// tag. Its topics map one to one onto the site's tags (<see cref="Tags.RiteTags"/>).
/// </summary>
public static class JevTags
{
    // Jev's topic, and the site's tag for it.
    public static readonly IReadOnlyDictionary<string, string> TopicTags = new Dictionary<string, string>
    {
        ["builds"] = "builds",
        ["dependencies"] = "dependencies",
        ["caches"] = "caches",
        ["restarts"] = "restarts",
        ["ai"] = "ai",
        ["testing"] = "testing",
        ["deployment"] = "deployment",
        ["debugging"] = "debugging",
        ["configuration"] = "configuration",
        ["infrastructure"] = "infrastructure",
        ["devices"] = "devices",
        ["version_control"] = "version-control",
        ["data"] = "data",
        ["security"] = "security",
        ["performance"] = "performance",
    };

    /// <summary>The tags for Jev's scores; None for scores Jev didn't give with these questions.</summary>
    public static Option<string[]> From(RiteSimilarity similarity, JevOptions jev)
        => Some(similarity)
            .Filter(scores => JevQuestions.IsVersion(scores.ScoresGeneratorVersion) && scores.Scores.Length >= JevQuestions.Topics.Count)
            .Map(scores => JevQuestions.Topics
                // The scores start with the topics' probabilities, in JevQuestions' order.
                .Select((topic, index) => (topic.Key, Probability: scores.Scores[index]))
                .Where(topic => TopicTags.ContainsKey(topic.Key) && topic.Probability >= jev.TagMinProbability)
                .OrderByDescending(topic => topic.Probability)
                .Take(jev.TagsPerRite)
                .Select(topic => TopicTags[topic.Key])
                .ToArray());
}
