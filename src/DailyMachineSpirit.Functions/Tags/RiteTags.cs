using LanguageExt;

namespace DailyMachineSpirit.Functions.Tags;

/// <summary>
/// The site's tags: a fixed vocabulary, so there's no "cache" next to "caches". The judge gives each draft its tags from
/// these (<see cref="Generation.Judging.IRiteJudge"/>), and they're stored with the draft and the rite it becomes.
/// </summary>
public static class RiteTags
{
    public static IReadOnlyList<Tag> All { get; } =
    [
        new("builds", "Builds", "builds, compilers, CI pipelines, failing or flaky checks"),
        new("dependencies", "Dependencies", "packages, dependencies, versions, lock files, upgrades"),
        new("caches", "Caches", "caches, temporary files, clean installs, leftover state"),
        new("restarts", "Restarts", "restarting or reinstalling apps, services, machines, routers"),
        new("ai", "AI", "AI assistants, prompts, generated code, vibe coding"),
        new("testing", "Testing", "tests, test coverage, mocks"),
        new("deployment", "Deployment", "deployments, releases, production, rollbacks"),
        new("debugging", "Debugging", "debugging, logs, errors, stack traces"),
        new("configuration", "Configuration", "configuration, environment variables, settings, feature flags"),
        new("infrastructure", "Infrastructure", "cloud, servers, containers, networks, DNS"),
        new("devices", "Devices", "hardware, devices, printers, cables, batteries"),
        new("version-control", "Version control", "version control, branches, merges, commits"),
        new("data", "Data", "databases, queries, migrations, backups"),
        new("security", "Security", "passwords, certificates, permissions, security"),
        new("performance", "Performance", "performance, timeouts, waiting, retries"),
    ];

    public static Option<Tag> Find(string key) => All.Where(tag => tag.Key == key).HeadOrNone();

    /// <summary>
    /// The subjects a searched word names: those with a word in their name that starts with it, ignoring case ("build" and
    /// "builds" name Builds, "control" Version control).
    /// </summary>
    public static List<Tag> Named(string word) => All.Where(tag => Names(tag, word)).ToList();

    /// <summary>Whether a searched word names the subject (<see cref="Named"/>).</summary>
    public static bool Names(Tag tag, string word)
        => tag.Label.Split(' ').Any(part => part.StartsWith(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>A rite's or draft's tags, in the order its judge gave them; keys no longer in the vocabulary are left out.</summary>
    public static List<Tag> Of(Option<string[]> keys)
        => keys.IfNone([]).Select(Find).Somes().ToList();
}
