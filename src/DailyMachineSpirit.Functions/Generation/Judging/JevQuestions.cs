namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// The questions Jev answers about each draft. Which software habit it's about, and what it asks its believers to do,
/// make its similarity scores: each answer is a probability for every option, so two rites are close when their
/// probabilities are.
/// </summary>
public static class JevQuestions
{
    /// <summary>Changes whenever a topic or act option changes: older scores then aren't comparable any more.</summary>
    public const string QuestionSet = "q1";

    // Every Jev model's name starts with it (jev-1.13.0), so another judge's version can't pass for Jev's.
    private const string ModelPrefix = "jev-";

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

    public static string Version(string model) => $"{model}/{QuestionSet}";

    /// <summary>Whether scores of this version are Jev's answers to these questions, whichever Jev model gave them.</summary>
    public static bool IsVersion(string scoresGeneratorVersion)
        => scoresGeneratorVersion.StartsWith(ModelPrefix, StringComparison.Ordinal)
            && scoresGeneratorVersion.EndsWith($"/{QuestionSet}", StringComparison.Ordinal);
}
