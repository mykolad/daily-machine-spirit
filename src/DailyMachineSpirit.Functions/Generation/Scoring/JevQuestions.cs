namespace DailyMachineSpirit.Functions.Generation.Scoring;

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
