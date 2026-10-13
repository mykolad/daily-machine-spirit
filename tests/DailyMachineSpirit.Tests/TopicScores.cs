using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Judging;

namespace DailyMachineSpirit.Tests;

/// <summary>Similarity scores as Jev gives them: these topics' probabilities (every other topic 0), then the acts'.</summary>
public static class TopicScores
{
    public static RiteSimilarity About(params (string Topic, float Probability)[] topics) => new()
    {
        ScoresGeneratorVersion = JevQuestions.Version("jev-1.13.0"),
        Scores =
        [
            .. JevQuestions.Topics.Select(topic => topics.Where(given => given.Topic == topic.Key).Select(given => given.Probability).FirstOrDefault()),
            .. JevQuestions.Acts.Select(_ => 0f),
        ],
        CreatedAtUtc = new DateTime(2026, 10, 9, 0, 1, 0, DateTimeKind.Utc),
    };
}
