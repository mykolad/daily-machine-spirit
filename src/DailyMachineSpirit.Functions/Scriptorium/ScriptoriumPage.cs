using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's HTML. Plain forms: every action is a button that posts, so it works with a keyboard and a screen
/// reader as it is, and without a script. <see cref="ScriptoriumScript"/> only shows the change in place. The colours and type follow the site's design tokens (docs: the design handoff).
/// </summary>
public static partial class ScriptoriumPage
{
    private const string Styles = """
        :root { color-scheme: dark; }
        * { box-sizing: border-box; }
        body { margin: 0; background: #100e0b; color: #ede3cf; font: 16px/1.5 Figtree, system-ui, sans-serif; }
        .frame { overflow-x: clip; }
        header, main { max-width: 820px; margin: 0 auto; padding: 28px clamp(16px, 4vw, 40px) 0; }
        main { padding-bottom: 72px; }
        footer { max-width: 600px; margin: 0 auto; padding: 0 16px 48px; text-align: center; }
        .motto { margin: 0 0 8px; font: 700 16px Cinzel, serif; letter-spacing: .08em; color: #c9a25a; }
        .disclaimer { margin: 0; font: 400 14px/1.6 Figtree, sans-serif; color: #b6a98f; }
        .brand { display: inline-flex; align-items: center; gap: 10px; padding: 4px 8px 4px 0; font: 700 16px Cinzel, serif; letter-spacing: .04em; color: #c9a25a; text-decoration: none; }
        .brand:hover { color: #f0d59a; }
        h1 { font: 700 clamp(30px, 4.4vw, 46px)/1.1 Cinzel, serif; margin: 24px 0 8px; }
        h2 { font: 700 26px/1.2 Cinzel, serif; margin: 0; }
        h3 { font: 700 21px/1.2 Cinzel, serif; margin: 6px 0 8px; }
        .lede { font: italic 400 20px/1.4 "EB Garamond", serif; color: #cdbd9e; margin: 0 0 16px; }
        .status:not(:empty) { background: #1a1612; border-left: 4px solid #c9a25a; padding: 12px 16px; border-radius: 12px; }
        section { margin-top: 44px; }
        .section-head { display: flex; flex-wrap: wrap; gap: 12px 24px; align-items: center; justify-content: space-between; margin-bottom: 8px; }
        .actions, .buttons { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
        .buttons .burn { margin-left: auto; }
        .pair { display: inline-flex; }
        .pair button { border-radius: 0; }
        .pair button:first-child { border-radius: 999px 0 0 999px; }
        .pair button:last-child { border-radius: 0 999px 999px 0; }
        .pair button:only-child { border-radius: 999px; }
        .pair button + button { margin-left: -1px; }
        .pair button:focus-visible { position: relative; z-index: 1; }
        .actions form { margin: 0; }
        .calendar, .judgments { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 16px; }
        .suggestions { list-style: none; padding: 0; margin: 12px 0 0; display: flex; flex-wrap: wrap; gap: 8px; }
        .rite { background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; padding: 20px clamp(16px, 3vw, 24px); scroll-margin-top: 16px; }
        .next { display: inline-block; margin: 8px 0 0; padding: 2px 10px; border: 1px solid #2e4248; border-radius: 999px; font: 500 13px "JetBrains Mono", monospace; color: #8cc4ae; }
        .ash { opacity: .92; }
        .acted { border-color: #f0b45a; animation: acted 1.6s ease-out; }
        @keyframes acted { from { box-shadow: 0 0 0 4px rgba(240, 180, 90, .55); } to { box-shadow: 0 0 0 4px rgba(240, 180, 90, 0); } }
        .kicker { margin: 0; font: 700 13px Cinzel, serif; letter-spacing: .16em; text-transform: uppercase; color: #c9a25a; }
        .placed { color: #f0b45a; }
        .resembles { margin: 0 0 12px; padding: 8px 12px; border-left: 4px solid #f0b45a; background: #241c12; border-radius: 8px; color: #ede3cf; }
        .text { font: 400 19px/1.55 "EB Garamond", serif; margin: 0 0 12px; }
        .truth { background: #141c1f; border: 1px solid #2e4248; border-radius: 16px; padding: 12px 16px; color: #dbe6e8; margin: 0 0 12px; }
        .truth-label { display: block; font: 500 14px "JetBrains Mono", monospace; letter-spacing: .14em; text-transform: uppercase; color: #8cc4ae; }
        code { font-family: "JetBrains Mono", monospace; font-size: .9em; background: #0b1214; color: #f0d58c; padding: 1px 6px; border-radius: 6px; }
        .text code { background: #2b2117; color: #f0d58c; }
        .meta, .when, .optional { color: #b6a98f; }
        .meta { margin: 0 0 12px; font-size: 15px; }
        .tags { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 12px; }
        .tag { padding: 2px 12px; border-radius: 999px; border: 1px solid rgba(201,162,90,.45); font: 600 14px/1.5 Figtree, sans-serif; color: #d9b874; }
        .fold summary { cursor: pointer; padding: 10px 0; font-weight: 600; color: #cdbd9e; }
        .fold summary .icon { width: 16px; height: 16px; vertical-align: -2px; margin: 0 4px; }
        .amend { max-width: 740px; }
        .amend label { display: block; margin: 20px 0 6px; font-weight: 600; }
        .hint { margin: 0 0 8px; font-size: 15px; color: #b6a98f; }
        .amend .buttons { margin-top: 20px; }
        .amend input[type=text], textarea { width: 100%; background: #100e0b; color: #ede3cf; border: 1px solid #b6a98f; border-radius: 12px; padding: 8px 12px; font: 16px/1.4 Figtree, sans-serif; margin-bottom: 8px; }
        button { display: inline-flex; align-items: center; gap: 8px; min-height: 46px; padding: 10px 18px 10px 14px; border-radius: 999px; font: 600 16px/1.2 Figtree, sans-serif; cursor: pointer; border: 1px solid transparent; }
        .icon { width: 18px; height: 18px; flex: none; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
        .primary { background: #c9a25a; color: #100e0b; }
        .button-link { display: inline-flex; align-items: center; gap: 8px; min-height: 46px; padding: 10px 18px 10px 14px; border-radius: 999px; border: 1px solid rgba(237,227,207,.22); font: 600 16px/1.2 Figtree, sans-serif; color: #ede3cf; text-decoration: none; }
        .button-link:hover { background: rgba(237,227,207,.08); border-color: rgba(237,227,207,.4); color: #ede3cf; }
        .primary:hover { background: #d6b170; }
        .outline, .secondary { background: transparent; color: #ede3cf; border-color: rgba(237,227,207,.22); }
        .outline:hover, .secondary:hover { background: rgba(237,227,207,.08); border-color: rgba(237,227,207,.4); }
        .danger { background: #3b1814; color: #f6cfc6; border-color: #d0604d; }
        .danger:hover { background: #4a1f19; }
        .judgments li { background: #1a1612; border-radius: 12px; padding: 10px 14px; }
        .record { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 8px; }
        .record li { background: #1a1612; border-radius: 12px; padding: 10px 14px; }
        .record time { display: block; font: 500 13px "JetBrains Mono", monospace; color: #b6a98f; }
        .record-link { margin: 0 0 16px; }
        .agreement { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 15px; }
        .agreement th, .agreement td { text-align: left; vertical-align: top; padding: 8px 10px 8px 0; border-bottom: 1px solid rgba(237,227,207,.12); }
        .agreement thead th { font-weight: 600; color: #b6a98f; }
        .judgments cite { font-style: normal; font-weight: 600; }
        .note { display: block; font: italic 18px/1.4 "EB Garamond", serif; color: #cdbd9e; }
        a { color: #d9b874; }
        a:hover { color: #f0d59a; }
        :focus-visible { outline: 2px solid #f0b45a; outline-offset: 4px; }
        .skip { position: absolute; left: -9999px; }
        .skip:focus { left: 16px; top: 16px; background: #c9a25a; color: #100e0b; padding: 8px 16px; border-radius: 999px; }
        .nowrap { white-space: nowrap; }
        .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
        @media (prefers-reduced-motion: reduce) { * { transition: none !important; animation: none !important; } }
        """;

    /// <param name="outcome">What the last action did (<see cref="ScriptoriumFunction"/>'s <c>done</c>), if anything.</param>
    /// <param name="rite">The draft it acted on, so the message can name it and say where it went.</param>
    /// <param name="name">How the Record shows the Scribe looking, with a way to choose or change it (#84); none on staging.</param>
    public static string Render(ScriptoriumView view, Option<string> outcome, Option<Guid> rite, Option<NameInTheRecord> name)
    {
        var status = outcome.Bind(done => Status(done, view, rite));
        var html = new StringBuilder();
        Head(html, "The Scriptorium");
        html.Append("""
            <main id="main">
            """);
        Heading(html, "The Scriptorium",
            "Unconsecrated rites await the judgment of the Scribes. Your notes teach the Augury what to choose when no Scribe is watching.");
        html.Append("""<p class="record-link"><a href="/scriptorium/precepts">The Precepts</a>: what the Machine Spirit has learned from your judgments, and whether the Scribes agree with the Augury. <a href="/scriptorium/record">The Record</a>: every change, and who made it.</p>""");
        name.IfSome(yours => html.Append(yours.Chosen
            ? $"""<p class="record-link">The Record calls you “{Encode(yours.Shown)}”. <a href="/scriptorium/name">Change your name</a></p>"""
            : $"""<p class="record-link">The Record shows you as “{Encode(yours.Shown)}”. <a href="/scriptorium/name">Choose a name</a> for it.</p>"""));
        html.Append($"""<p class="status" role="status">{status.IfNone(string.Empty)}</p>""");

        html.Append($"""
            <section aria-labelledby="calendar-heading">
            <div class="section-head">
            <h2 id="calendar-heading">The Liturgical Calendar</h2>
            <div class="actions">
            <form method="post" action="/scriptorium/summon"><button title="{Encode(About("summon"))}" class="primary">{ScriptoriumIcons.Summon}Summon New Rites</button></form>
            """);
        if (view.Calendar.Any(entry => entry.PlacedByScribes))
        {
            html.Append($"""<form method="post" action="/scriptorium/augury"><button title="{Encode(About("augury"))}" class="outline">{ScriptoriumIcons.Augury}Let the Augury Decide</button></form>""");
        }
        html.Append("</div></div>");

        if (view.Calendar.Count == 0)
        {
            html.Append("<p>No rite waits. Summon new rites, or the Machine Spirit writes tonight's on the spot.</p>");
        }
        else
        {
            html.Append($"<p>{view.Calendar.Count} unconsecrated {(view.Calendar.Count == 1 ? "rite waits" : "rites wait")}. The Scribes' order comes first; the Augury orders the rest.</p>");
            html.Append($"""<p class="meta">{Subjects(view.Calendar.Select(entry => entry.Draft))}</p>""");
            html.Append("""<ol class="calendar">""");
            for (var index = 0; index < view.Calendar.Count; index++)
            {
                CalendarEntry(html, view.Calendar[index], place: index + 1, isLast: index == view.Calendar.Count - 1, view.Today, rite);
            }
            html.Append("</ol>");
        }
        html.Append("</section>");

        html.Append("""<section aria-labelledby="ashes-heading"><h2 id="ashes-heading">The Ashes</h2>""");
        if (view.Ashes.Count == 0)
        {
            html.Append("<p>Nothing has been consigned to the flames.</p>");
        }
        else
        {
            html.Append("""<ul class="calendar">""");
            foreach (var draft in view.Ashes)
            {
                Ash(html, draft, rite);
            }
            html.Append("</ul>");
        }
        html.Append("</section>");

        html.Append("""<section aria-labelledby="judgments-heading"><h2 id="judgments-heading">Recent judgments</h2>""");
        if (view.Decisions.Count == 0)
        {
            html.Append("<p>The Scribes have not judged yet.</p>");
        }
        else
        {
            html.Append("""<ul class="judgments">""");
            foreach (var decision in view.Decisions)
            {
                html.Append($"""<li><span class="when">{Date(DateOnly.FromDateTime(decision.DecidedAtUtc))}</span> {Encode(Done(decision.Action))}: <cite>{Encode(decision.Title)}</cite>""");
                decision.Note.IfSome(note => html.Append($"""<span class="note">“{Encode(note)}”</span>"""));
                html.Append("</li>");
            }
            html.Append("</ul>");
        }
        html.Append("</section></main>");
        Footer(html, withScript: true);
        return html.ToString();
    }

    public static string RenderError()
    {
        var html = new StringBuilder();
        Head(html, "The Scriptorium is silent");
        html.Append("""
            <main id="main">
            """);
        Heading(html, "The Scriptorium is silent", "The Machine Spirit could not open the records. Try again in a moment.");
        html.Append("""
            <p><a href="/scriptorium">Return to the Scriptorium</a></p>
            </main>
            """);
        Footer(html, withScript: false);
        return html.ToString();
    }

    /// <summary>
    /// The page for amending a waiting draft's words (#8). Plain form posts, without the calendar's script: saving goes
    /// back to the calendar, and asking for a rewrite comes back here with the rewrite in the fields, unsaved.
    /// </summary>
    /// <param name="words">What the fields hold: the draft's words, or what the Scribe or the rewrite put there.</param>
    /// <param name="message">Why it came back: a rule the words break, or that the Machine Spirit rewrote them.</param>
    public static string RenderAmend(AmendableDraft amendable, RiteContent words, string note, Option<string> message)
    {
        var draft = amendable.Draft;
        var id = draft.Id.ToString("D");
        var html = new StringBuilder();
        Head(html, $"Amend “{draft.Writing.Title}”");
        html.Append("""<main id="main">""");
        Heading(html, "Amend the Rite",
            "Change its words by hand, or write a note and let the Machine Spirit rewrite them. Whatever you save is judged again.");
        html.Append($"""
            <p class="status" role="status">{message.Map(Encode).IfNone(string.Empty)}</p>
            <form method="post" class="amend" action="/scriptorium/drafts/{id}/amend">
            <input type="hidden" name="etag" value="{Encode(amendable.ETag)}">
            <p class="kicker">{Kind(draft.Writing.Kind)} · written by {Encode(draft.Writing.GeneratedByModel)}</p>
            <label for="amend-title">Title</label>
            <input type="text" id="amend-title" name="title" value="{Encode(words.Title)}" maxlength="{RiteWriting.MaxTitleLength}" required>
            <label for="amend-text">Text</label>
            <textarea id="amend-text" name="text" rows="6" maxlength="{RiteWriting.MaxTextLength}" required>{Encode(words.Text)}</textarea>
            <label for="amend-truth">Heretical Truth</label>
            <textarea id="amend-truth" name="truth" rows="3" maxlength="{RiteWriting.MaxHereticalTruthLength}" aria-describedby="amend-code-hint" required>{Encode(words.HereticalTruth)}</textarea>
            <p class="hint" id="amend-code-hint">Commands, flags and file names go in `backticks`, as the Machine Spirit writes them.</p>
            <label for="amend-note">Note</label>
            <p class="hint" id="amend-note-hint">Why it needs amending. The Machine Spirit follows it when asked to rewrite, and the Augury learns from it.</p>
            <textarea id="amend-note" name="note" rows="3" maxlength="{ScribeDecision.MaxNoteLength}" aria-describedby="amend-note-hint">{Encode(note)}</textarea>
            <div class="buttons">
            <button class="primary">{ScriptoriumIcons.Save}Save the Amendment</button>
            <button class="secondary" formaction="/scriptorium/drafts/{id}/rewrite">{ScriptoriumIcons.Summon}Ask the Machine Spirit to Rewrite</button>
            <a class="button-link" href="/scriptorium#rite-{id}">Leave It as It Was</a>
            </div>
            </form>
            </main>
            """);
        Footer(html, withScript: false);
        return html.ToString();
    }

    /// <summary>
    /// The Record (#9): every change to the drafts and rites, and who made it, newest first; or one draft's, from its
    /// first draft to its publishing.
    /// </summary>
    /// <param name="names">The names the Scribes chose (#84); the others show by the end of their ids.</param>
    public static string RenderRecord(IReadOnlyList<AuditEntry> entries, IReadOnlyList<ScribeName> names, Option<Guid> draft)
    {
        var html = new StringBuilder();
        Head(html, "The Record");
        html.Append("""<main id="main">""");
        Heading(html, "The Record",
            "Every change to the calendar, the rites and the Precepts, and who made it, newest first: the Scribes' actions and the Machine Spirit's own.");
        html.Append(draft.Match(
            Some: _ => """<p>One rite's record. <a href="/scriptorium/record">The whole record</a> · <a href="/scriptorium">Back to the Scriptorium</a></p>""",
            None: () => """<p><a href="/scriptorium">Back to the Scriptorium</a></p>"""));
        if (entries.Count == 0)
        {
            html.Append("<p>Nothing is recorded yet.</p>");
        }
        else
        {
            html.Append("""<ol class="record">""");
            foreach (var entry in entries)
            {
                html.Append($"""<li><time datetime="{entry.AtUtc:yyyy-MM-ddTHH:mm:ssZ}">{When(entry.AtUtc)}</time> {Encode(ScribeNames.Of(entry.Actor, names))} {Did(entry)}</li>""");
            }
            html.Append("</ol>");
        }
        html.Append("</main>");
        Footer(html, withScript: false);
        return html.ToString();
    }

    /// <param name="acted">The rite the last action was on, marked so the Scribe's eye finds where it went.</param>
    private static void CalendarEntry(StringBuilder html, CalendarEntry entry, int place, bool isLast, DateOnly today, Option<Guid> acted)
    {
        var draft = entry.Draft;
        var id = draft.Id.ToString("D");
        html.Append($"""<li><article class="rite{Acted(draft, acted)}" id="rite-{id}" aria-labelledby="title-{id}">""");
        html.Append($"""<p class="kicker"><time datetime="{entry.PublishesOnUtc:yyyy-MM-dd}">{Date(entry.PublishesOnUtc)}</time> · {Kind(draft.Writing.Kind)}</p>""");
        // Today's own rite is only still due when the midnight run failed; its retry publishes it.
        if (place == 1)
        {
            html.Append($"""<p class="next">{(entry.PublishesOnUtc > today ? "Publishes at 00:00 UTC" : "Publishes next")}</p>""");
        }
        RiteBody(html, draft, id);

        // The Augury's own place for a draft only says something where the Scribes put it elsewhere.
        var augury = draft.Augury.Map(judged => entry.PlacedByScribes && entry.AuguryPlace != place
                ? $"The Augury: {judged.Quality:P0}, it would place it #{entry.AuguryPlace}"
                : $"The Augury: {judged.Quality:P0}")
            .IfNone("Not yet judged by the Augury");
        var placedBy = entry.PlacedByScribes ? """<span class="placed">Placed by the Scribes</span> · """ : string.Empty;
        html.Append($"""<p class="meta">{placedBy}{Encode(augury)} · written by <span class="nowrap">{Encode(draft.Writing.GeneratedByModel)}</span>{(draft.Writing.Edited ? ", amended by a Scribe" : string.Empty)} · <a href="/scriptorium/record?draft={id}">its record</a></p>""");
        // Written again and again, it still repeated a rite: the Scribes decide whether it's new enough.
        draft.Resembles.IfSome(title => html.Append($"""<p class="resembles">Much like “{WithCode(title)}”, already written.</p>"""));

        var reorder = new List<string>();
        if (place > 1)
        {
            reorder.Add(Button(id, draft.Writing.Title, "exalt", ScriptoriumIcons.Exalt, "Exalt", "secondary"));
        }
        if (!isLast)
        {
            reorder.Add(Button(id, draft.Writing.Title, "humble", ScriptoriumIcons.Humble, "Humble", "secondary"));
        }
        DecisionForm(html, id,
        [
            // Anointing the first is useful too: it keeps it first, ahead of a better draft a refill may bring.
            Button(id, draft.Writing.Title, "anoint", ScriptoriumIcons.Anoint, "Anoint", "primary"),
            .. reorder.Count == 0 ? [] : new[] { $"""<span class="pair" role="group" aria-label="Reorder">{string.Concat(reorder)}</span>""" },
            AmendLink(id, draft.Writing.Title),
            // Only where judging couldn't score it: judging a judged draft again would change nothing its words did.
            .. draft.Augury.IsSome ? [] : new[] { Button(id, draft.Writing.Title, "judge", ScriptoriumIcons.Augury, "Judge Again", "secondary") },
            Button(id, draft.Writing.Title, "burn", ScriptoriumIcons.Burn, "Consign to the Flames", "danger burn"),
        ]);
        html.Append("</article></li>");
    }

    private static void Ash(StringBuilder html, Draft draft, Option<Guid> acted)
    {
        var id = draft.Id.ToString("D");
        html.Append($"""<li><article class="rite ash{Acted(draft, acted)}" id="rite-{id}" aria-labelledby="title-{id}">""");
        html.Append($"""<p class="kicker">{Kind(draft.Writing.Kind)}""");
        draft.BurnedAtUtc.IfSome(burned => html.Append($" · burned {Date(DateOnly.FromDateTime(burned))}"));
        html.Append("</p>");
        RiteBody(html, draft, id);
        DecisionForm(html, id, [Button(id, draft.Writing.Title, "restore", ScriptoriumIcons.Restore, "Restore from the Ashes", "secondary")]);
        html.Append("</article></li>");
    }

    private static void RiteBody(StringBuilder html, Draft draft, string id)
    {
        html.Append($"""<h3 id="title-{id}">{WithCode(draft.Writing.Title)}</h3>""");
        // Under the title, where an eye matches them against the calendar's subjects above. Not links: following one
        // would leave the Scriptorium for the public tag page.
        var tags = RiteTags.Of(draft.Tags);
        if (tags.Count > 0)
        {
            var labels = string.Concat(tags.Select(tag => $"""<span class="tag">{Encode(tag.Label)}</span>"""));
            html.Append($"""<p class="tags"><span class="visually-hidden">Filed under </span>{labels}</p>""");
        }
        html.Append($"""<p class="text">{WithCode(draft.Writing.Text)}</p>""");
        html.Append($"""<p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(draft.Writing.HereticalTruth)}</p>""");
    }

    // One form per draft, so its note goes with whichever button is pressed. The note is folded away: twenty open boxes
    // would make the page very long, and most decisions go without one.
    private static void DecisionForm(StringBuilder html, string id, IEnumerable<string> buttons)
        => html.Append($"""
            <form method="post" class="decide">
            <details class="fold">
            <summary>{ScriptoriumIcons.Note}Note for the Augury <span class="optional">(optional)</span></summary>
            <label class="visually-hidden" for="note-{id}">Note for the Augury</label>
            <textarea id="note-{id}" name="note" rows="2" maxlength="{ScribeDecision.MaxNoteLength}"></textarea>
            </details>
            <div class="buttons">{string.Concat(buttons)}</div>
            </form>
            """);

    // Each button names the rite for screen readers, since every entry has the same buttons.
    private static string Button(string id, string title, string action, string icon, string label, string style)
        => $"""<button title="{Encode(About(action))}" class="{style}" formaction="/scriptorium/drafts/{id}/{action}">{icon}{label}<span class="visually-hidden">: {Encode(title)}</span></button>""";

    // A link, not a button: it opens the amending page, and nothing changes until that page's form is saved.
    private static string AmendLink(string id, string title)
        => $"""<a class="button-link" title="{Encode(About("amend"))}" href="/scriptorium/drafts/{id}/amend">{ScriptoriumIcons.Note}Amend<span class="visually-hidden">: {Encode(title)}</span></a>""";

    // What an action does, shown when the pointer rests on its button: the names are the rite's own words, and a Scribe
    // who acts once a month needn't remember what "Let the Augury Decide" means. Each place in the calendar is a day.
    private static string About(string action) => action switch
    {
        "summon" => "Writes new drafts in the background until the calendar is full again. Look again in a few minutes.",
        "augury" => "Forgets the Scribes' order: the Augury orders every waiting rite again, by quality and variety.",
        "anoint" => "Makes this rite the next one published, ahead of the Augury's picks.",
        "exalt" => "Moves this rite one day earlier. It and every rite above it then keep the Scribes' order.",
        "humble" => "Moves this rite one day later, swapping it with the next; both then keep the Scribes' order.",
        "burn" => "Takes this rite out of the calendar, into the Ashes. Nothing is deleted: it can be restored.",
        "amend" => "Changes its words by hand, or asks the Machine Spirit to rewrite them as your note says. It's judged again when saved.",
        "judge" => "Asks the Augury to judge this rite now, as the next refill would: judging it failed, or was off, when it was written.",
        _ => "Puts this rite back in the calendar, where the Augury places it.",
    };

    // On an altar, as the archive's heading is (Pages.AltarHeading): the glow behind it, and candles beside it on a wide
    // screen. The script replaces it with the rest of the page after an action, which changes nothing in it.
    private static void Heading(StringBuilder html, string title, string lede)
        => html.Append($"""
            <div class="altar-head">
            <div>
            <h1>{Encode(title)}</h1>
            <p class="lede">{Encode(lede)}</p>
            </div>
            {Pages.Svg.AltarCandles}
            {Pages.Svg.Traces}
            </div>
            """);

    // Every page carries the site's motto and the Games Workshop disclaimer (CLAUDE.md, Ground rules).
    // The amending page goes without the script, whose in-place actions belong to the calendar: its forms simply post.
    private static void Footer(StringBuilder html, bool withScript)
        => html.Append("""
            <footer>
            <p class="motto">Knowledge is lost. The rituals remain.</p>
            <p class="disclaimer">An unofficial fan satire, not affiliated with or endorsed by Games Workshop. Warhammer 40,000 and related names are trademarks of Games Workshop Ltd. Prayers are generated by AI.</p>
            </footer>
            </div>
            """).Append(withScript ? $"<script>{ScriptoriumScript.Js}</script>" : string.Empty).Append("</body></html>");

    private static void Head(StringBuilder html, string title)
    {
        html.Append($"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <link rel="icon" type="image/svg+xml" href="{Pages.Svg.Favicon}">
            <title>{Encode(title)} · The Daily Machine Spirit</title>
            <style>{Pages.Styles.FontFaces}{Styles}{Pages.Styles.Altar}</style>
            </head>
            <body>
            <a class="skip" href="#main">Skip to content</a>
            <div class="frame">
            <header><a class="brand" href="/">{Pages.Svg.BrandCog}The Daily Machine Spirit</a></header>
            """);
    }

    // What the waiting drafts are about, by their tags, so the Scribes see a calendar heavy on one subject.
    private static string Subjects(IEnumerable<Draft> drafts)
    {
        var tagged = drafts.Select(draft => RiteTags.Of(draft.Tags)).ToList();
        var counts = tagged
            .SelectMany(tags => tags)
            .GroupBy(tag => tag.Label)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} ({group.Count()})")
            .ToList();
        var untagged = tagged.Count(tags => tags.Count == 0);
        if (untagged > 0)
        {
            counts.Add($"untagged ({untagged})");
        }
        return Encode($"Subjects waiting: {string.Join(", ", counts)}.");
    }

    private static string Acted(Draft draft, Option<Guid> acted) => acted.Exists(id => id == draft.Id) ? " acted" : string.Empty;

    private static string Kind(RiteKind kind) => kind == RiteKind.Prayer ? "A Prayer" : "A Ritual";

    private static string Date(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    // What the last action did, as HTML. It names the rite and where it went when the rite is still on the page (it may
    // have been published or moved again since), and links a burned rite to its place in the Ashes, to restore it.
    private static Option<string> Status(string outcome, ScriptoriumView view, Option<Guid> rite)
    {
        var entry = rite.Bind(id => view.Calendar.Where(waiting => waiting.Draft.Id == id).HeadOrNone());
        var ash = rite.Bind(id => view.Ashes.Where(burned => burned.Id == id).HeadOrNone());
        var title = (entry.Map(waiting => waiting.Draft) | ash).Map(draft => $"“{WithCode(draft.Writing.Title)}”");
        var date = entry.Map(waiting => Date(waiting.PublishesOnUtc));
        var augury = entry.Bind(waiting => waiting.Draft.Augury)
            .Map(judged => $", {judged.Quality.ToString("P0", CultureInfo.InvariantCulture)}").IfNone(string.Empty);
        string Named(string done) => title.Map(name => $"{done} {name}").IfNone(done);
        var restore = ash.Map(burned => $""" <a href="#rite-{burned.Id:D}">Restore it from the Ashes</a>.""").IfNone(string.Empty);
        return outcome switch
        {
            "anoint" => $"{Named("Anointed")}: it goes out next{date.Map(day => $", {day}").IfNone(string.Empty)}.",
            "exalt" => $"{Named("Exalted")}{date.Map(day => $" to {day}").IfNone(string.Empty)}.",
            "humble" => $"{Named("Humbled")}{date.Map(day => $" to {day}").IfNone(string.Empty)}.",
            "burn" => $"{Named("Consigned")} to the flames.{restore}",
            "burn-last" => $"{Named("Consigned")} to the flames. No rite waits now, so new ones are being summoned: look again in a few minutes.{restore}",
            "restore" => $"{Named("Restored")} from the ashes{date.Map(day => $": it waits for {day}").IfNone(string.Empty)}.",
            "summon" => "New rites are being summoned: look again in a few minutes.",
            "amend" => $"{Named("Amended")}: judged again, it waits{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "amend-unjudged" => $"{Named("Amended")}, but it couldn't be judged again, so it waits unjudged{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "judged" => $"{Named("Judged")} again{augury}: the Augury places it{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "still-unjudged" => $"{Named("Still")} can't be judged: the judge is failing or off. The next refill tries again.",
            "augury" => "The Augury orders every draft again.",
            "changed" => "Another Scribe changed the calendar meanwhile. Look again before deciding.",
            "unmoved" => "It was already there, so nothing moved. Another Scribe may have moved it: look again.",
            "note-too-long" => "That note is too long, so nothing was changed. Shorten it and try again.",
            "named" => "The Record shows your name now, on every change you made.",
            _ => None,
        };
    }

    private static string Done(ScribeAction action) => action switch
    {
        ScribeAction.Anoint => "Anointed",
        ScribeAction.Exalt => "Exalted",
        ScribeAction.Humble => "Humbled",
        ScribeAction.Burn => "Consigned to the flames",
        ScribeAction.Amend => "Amended",
        _ => "Restored from the ashes",
    };

    // What an entry records, after who: the rite (or precept) named by its words then, a rite linking to its own record.
    private static string Did(AuditEntry entry)
    {
        var rite = entry.DraftId.Match(
            Some: id => $"""<a href="/scriptorium/record?draft={id:D}">“{WithCode(entry.Title)}”</a>""",
            None: () => $"“{WithCode(entry.Title)}”");
        var what = entry.Action switch
        {
            AuditAction.Anoint => $"anointed {rite}",
            AuditAction.Exalt => $"exalted {rite}",
            AuditAction.Humble => $"humbled {rite}",
            AuditAction.Burn => $"consigned {rite} to the flames",
            AuditAction.Restore => $"restored {rite} from the ashes",
            AuditAction.Amend => $"amended {rite}",
            AuditAction.Summon => "summoned new rites",
            AuditAction.LetTheAuguryDecide => "let the Augury decide",
            AuditAction.Write => $"wrote {rite}",
            AuditAction.Distil => """distilled <a href="/scriptorium/precepts">the Precepts</a>""",
            AuditAction.AddPrecept => $"added the precept {rite}",
            AuditAction.EditPrecept => $"edited the precept {rite}",
            AuditAction.PinPrecept => $"pinned the precept {rite}",
            AuditAction.UnpinPrecept => $"unpinned the precept {rite}",
            AuditAction.DeletePrecept => $"deleted the precept {rite}",
            AuditAction.JudgeAgain => $"judged {rite} again",
            AuditAction.ChooseName => $"chose the name {rite}",
            _ => $"published {rite}{entry.RiteNumber.Map(number => $" as NO. {number}").IfNone(string.Empty)}",
        };
        return what + entry.Detail.Map(detail => $""" <span class="when">({Encode(detail)})</span>""").IfNone(string.Empty);
    }

    private static string When(DateTime atUtc) => atUtc.ToString("ddd d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The rites put commands and file names in `backticks`.
    private static string WithCode(string text) => InlineCode().Replace(Encode(text), "<code>$1</code>");

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCode();
}
