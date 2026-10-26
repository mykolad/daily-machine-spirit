namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// What the site is, how a rite is read, and how to consult the Machine Spirit from an assistant (the connector, the
/// skill) or code (the rites API). The questions are the shrine's, the answers plain.
/// </summary>
public static class AboutPage
{
    private const string ConnectorAddress = "https://mcp.dailymachinespirit.fyi/mcp";

    private const string Repository = "https://github.com/mykolad/daily-machine-spirit";

    // Into the user's own skills, as skills/README.md says; it has PowerShell's too, and the Claude app's way.
    private const string SkillInstall = $"""
        git clone --depth 1 {Repository}.git
        mkdir -p ~/.claude/skills && cp -r daily-machine-spirit/skills/consult-the-machine-spirit ~/.claude/skills/
        """;

    private const string Description = "What The Daily Machine Spirit is, and how to consult it from your assistant.";

    public static string Render()
        => Layout.Page("About", Description, Section.About, $"""
            <article class="about">
            <h1>About</h1>
            <p class="tagline">{Masthead.Tagline}</p>

            <h2>What is this shrine?</h2>
            <p>A satire of coding by ritual. In a far future that forgot how its machines work, developers keep their habits as rites to the Machine Spirit: rerun the build until it passes, clear the cache, restart and hope. Every day at 00:00 UTC the shrine publishes one, a prayer or a ritual, chosen by its moderators, the Scribes.</p>
            <p>Each rite hides its Heretical Truth under a wax seal: in a sentence or two, what really happens, and the real fix. Break the seal to read it.</p>

            <h2>How is a rite read?</h2>
            <ul>
            <li>The date under its title is the far future’s reckoning. Point at it or tap it for the day as we count it.</li>
            <li>Blessed or Heresy: sign in with GitHub to vote, once a rite. You can change your vote or take it back.</li>
            <li><a href="{Paths.Archive}">The Archive</a> keeps every rite, to search or browse by subject.</li>
            </ul>

            <h2>Can my assistant consult the Machine Spirit?</h2>
            <p>Yes. Tell it what’s wrong (“my build fails, then passes when I rerun it”), and it prescribes a rite or two in a tech-priest’s voice, with their links, then offers to reveal the Heretical Truth, if you dare.</p>
            <h3>In the Claude or ChatGPT app: the connector</h3>
            <p>Add a custom connector (a remote MCP server) with this address. It needs no sign-in.</p>
            {Incantation(ConnectorAddress, "Copy address")}
            <p>Then ask, for example, “Consult the Machine Spirit: my cache keeps serving the old page.” The connector only fetches the rites, so your trouble stays in your conversation, and the assistant breaks a seal only when you ask.</p>
            <h3>In Claude Code: the skill</h3>
            <p>Copy the skill into your skills, in bash or zsh:</p>
            {Incantation(SkillInstall, "Copy commands")}
            <p>Then ask as above. Your own assistant chooses the rite from the list it fetches, so your trouble never leaves your machine. For PowerShell, or for the skill in the Claude app, see <a href="{Repository}/blob/master/skills/README.md">the skill’s instructions</a>.</p>

            <h2>Can other machines read the rites?</h2>
            <p>Yes. Every published rite is at <code>{Paths.RitesApi}</code>, open to any site, each truth sealed until it’s asked for. <a href="{Repository}/blob/master/docs/api.md">The API’s documentation</a> describes it.</p>

            <h2>What does the shrine remember of me?</h2>
            <p>Nothing, unless you vote. A vote keeps a one-way key made from your GitHub account’s id: never your name, email or GitHub token. No ads, and no trackers.</p>

            <h2>A rite has erred?</h2>
            <p>If a rite is wrong, offensive or broken, or the site is, <a href="{Repository}/issues">open an issue on GitHub</a>.</p>
            </article>
            """);

    // What to type or paste, as a terminal shows it, with a button that copies it as it is.
    private static string Incantation(string text, string copyLabel)
        => $"""
            <div class="incantation">
            <pre><code>{Html.Encode(text)}</code></pre>
            <button type="button" class="button button-ghost" data-copy="value" data-text="{Html.Encode(text)}">{Svg.CopyIcon}{copyLabel}</button>
            </div>
            """;
}
