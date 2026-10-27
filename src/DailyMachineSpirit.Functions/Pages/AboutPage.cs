namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Catechism: what the site is, how a rite is read, and how to consult the Machine Spirit from a familiar (the
/// connector, the skill) or code (the rites API), as numbered questions on the Archive's parchment. The questions are
/// the shrine's, the answers plain.
/// </summary>
public static class AboutPage
{
    private const string ConnectorAddress = "https://mcp.dailymachinespirit.fyi/mcp";

    private const string Repository = "https://github.com/mykolad/daily-machine-spirit";

    private const string Description = "What The Daily Machine Spirit is, and how to consult it from your AI assistant.";

    // The margins' code, as on Today's parchment: "READ THE CODE" in hex and "KNOW" in binary.
    private const string LeftCode = "52 45 41 44 20 54 48 45 20 43 4F 44 45";
    private const string RightCode = "01001011 01001110 01001111 01010111";

    // Enough to run down the whole sheet, which is long; the margin cuts it where the sheet ends.
    private const int CodeCopies = 8;

    // Into the user's own skills, as skills/README.md says; it has PowerShell's too, and the Claude app's way.
    private static readonly string[] SkillCommands =
    [
        $"git clone --depth 1 {Repository}.git",
        "mkdir -p ~/.claude/skills",
        "cp -r daily-machine-spirit/skills/consult-the-machine-spirit ~/.claude/skills/",
    ];

    private static readonly string[] Numerals = ["I", "II", "III", "IV", "V", "VI"];

    /// <param name="now">Today's Imperial date is the example of how one is read.</param>
    public static string Render(DateTimeOffset now)
    {
        var questions = new (string Question, string Answer)[]
        {
            ("What is this shrine?", """
                <p>A satire of coding by ritual. In a far future that forgot how its machines work, developers keep their habits as rites to the Machine Spirit: rerun the build until it passes, clear the cache, restart and hope. Every day at 00:00 UTC the shrine publishes one rite, a prayer or a ritual. An AI writes them; the shrine’s moderators, the Scribes, choose which are published.</p>
                <p>Each rite hides its Heretical Truth under a wax seal: in a sentence or two, what really happens, and the real fix. Break the seal to read it.</p>
                """),
            ("How is a rite read?", $"""
                <ul role="list">
                <li>The date under its title is the far future’s reckoning: {ImperialDate.WithTheDay(DateOnly.FromDateTime(now.UtcDateTime), "catechism-day", "catechism-date")} counts the year’s thousandths passed, then the year, then the millennium. Point at it or tap it for the day as we count it.</li>
                <li>Blessed or Heresy: your verdict on a rite. Sign in with GitHub to vote, once a rite. You can change your vote or take it back.</li>
                <li><a href="{Paths.Archive}">The Archive</a> keeps every rite, to search or browse by subject.</li>
                </ul>
                """),
            ("Can my familiar consult the Machine Spirit?", $"""
                <p>Yes, if it’s an AI assistant such as Claude or ChatGPT. Tell it what’s wrong (“my build fails, then passes when I rerun it”), and it prescribes a rite or two in a tech-priest’s voice, with their links, then offers to reveal the Heretical Truth, if you dare.</p>
                <h3><span class="answer-name">The connector</span> <span class="answer-place">in the Claude or ChatGPT app</span></h3>
                <p>Add a custom connector (a remote MCP server) with this address, in your app’s connector settings. It needs no sign-in.</p>
                {Incantation("The address", "Copy address", [ConnectorAddress])}
                <p>Then ask, for example, “Consult the Machine Spirit: my cache keeps serving the old page.” The connector only fetches the rites, so the shrine never hears your trouble, and your familiar breaks a seal only when you ask.</p>
                <h3><span class="answer-name">The skill</span> <span class="answer-place">in Claude Code</span></h3>
                <p>Copy the skill into your skills folder. In bash or zsh:</p>
                {Incantation("The incantation · bash, zsh", "Copy commands", SkillCommands)}
                <p>Then ask as above. Your familiar chooses the rite from the list it fetches, so your trouble is never sent to the shrine. For PowerShell, or for the skill in the Claude app, see <a href="{Repository}/blob/master/skills/README.md">the skill’s instructions</a>.</p>
                """),
            ("Can other machines read the rites?", $"""
                <p>Yes. Every published rite is at <code>{Paths.RitesApi}</code>, open to any site. Each Heretical Truth is left out unless it’s asked for; <a href="{Repository}/blob/master/docs/api.md">the API’s documentation</a> says how.</p>
                """),
            ("What does the shrine remember of me?", """
                <p>Nothing, unless you vote. A vote keeps a one-way key made from your GitHub account’s id: never your name, email or GitHub token. Signing in keeps a cookie for up to 30 days, so you stay signed in. No ads, and no trackers: the only measuring is Cloudflare’s, of how fast pages load, as the foot of the page says.</p>
                """),
            ("Has a rite erred?", $"""
                <p>If a rite is wrong, offensive or broken, or the site is, <a href="{Repository}/issues">open an issue on GitHub</a>.</p>
                """),
        };
        var numbered = questions.Select((question, index) => (Number: index + 1, Numeral: Numerals[index], question.Question, question.Answer)).ToList();
        return Layout.Page("The Catechism", Description, Section.About, $"""
            {Masthead.Render(isHeading: false)}
            <div class="hero catechism-hero">
            <article class="sheet catechism">
            <span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>
            {Svg.Corners}
            <span class="margin-text margin-left" aria-hidden="true">{Repeated(LeftCode)}</span>
            <span class="margin-text margin-right" aria-hidden="true">{Repeated(RightCode)}</span>
            <div class="sheet-inner">
            <header class="sheet-head">
            <p class="sheet-kicker">Questions of the faithful</p>
            <h1 class="sheet-title">The Catechism</h1>
            <p class="sheet-sub">What the shrine is, and how to consult it from your familiar.</p>
            {Svg.RubricDivider}
            </header>
            <div class="questions">
            {string.Concat(numbered.Select(question => $"""
                <section class="question" id="q-{question.Number}">
                <span class="question-number" aria-hidden="true">{question.Numeral}.</span>
                <div class="answer">
                <h2>{question.Question}</h2>
                {question.Answer}
                </div>
                </section>
                """))}
            </div>
            </div>
            </article>
            {ArchiveSheet.Margins($"""
                <aside class="question-index" aria-label="Questions">
                <p class="margin-label" aria-hidden="true">The questions</p>
                <ol role="list">{string.Concat(numbered.Select(question => $"""<li><a href="#q-{question.Number}"><span>{question.Question}</span><span class="index-number" aria-hidden="true">{question.Numeral}</span></a></li>"""))}</ol>
                </aside>
                """, string.Empty)}
            {Svg.Traces}
            </div>
            """);
    }

    // What to type or paste, as a terminal shows it, under a bar with its name and a button that copies it as it is: the
    // commands without their prompts. An address wraps on a narrow screen; commands scroll instead, since a wrapped line
    // would read as two, and their block takes focus, so a keyboard can scroll it too.
    private static string Incantation(string label, string copyLabel, IReadOnlyList<string> lines)
    {
        var pre = lines.Count > 1
            ? (Class: "incantation incantation-lines", Open: """<pre tabindex="0" aria-label="Commands">""",
                Code: string.Join("\n", lines.Select(line => $"""<span class="prompt" aria-hidden="true">$ </span>{Html.Encode(line)}""")))
            : (Class: "incantation", Open: "<pre>", Code: Html.Encode(lines[0]));
        return $"""
            <div class="{pre.Class}">
            <div class="incantation-bar">
            <span class="incantation-label">{label}</span>
            <button type="button" class="button button-ghost" data-copy="value" data-text="{Html.Encode(string.Join("\n", lines))}">{Svg.CopyIcon}{copyLabel}</button>
            </div>
            {pre.Open}<code>{pre.Code}</code></pre>
            </div>
            """;
    }

    private static string Repeated(string code) => string.Join(" · ", Enumerable.Repeat(code, CodeCopies));
}
