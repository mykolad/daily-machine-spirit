using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's HTML. Plain forms, no script: every action is a button that posts, so it works with a keyboard and
/// a screen reader as it is. The colours and type follow the site's design tokens (docs: the design handoff).
/// </summary>
public static partial class ScriptoriumPage
{
    public static string Render(ScriptoriumView view, Option<string> status)
    {
        var html = new StringBuilder();
        Head(html, "The Scriptorium");
        html.Append("""
            <main id="main">
            <h1>The Scriptorium</h1>
            <p class="lede">Unconsecrated rites await the judgment of the Scribes. Your notes teach the Augury what to choose when no Scribe is watching.</p>
            """);
        html.Append($"""<p class="status" role="status">{status.Map(Encode).IfNone(string.Empty)}</p>""");

        html.Append("""
            <section aria-labelledby="calendar-heading">
            <div class="section-head">
            <h2 id="calendar-heading">The Liturgical Calendar</h2>
            <div class="actions">
            <form method="post" action="/scriptorium/summon"><button class="primary">Summon New Rites</button></form>
            """);
        if (view.Calendar.Any(entry => entry.PlacedByScribes))
            html.Append("""<form method="post" action="/scriptorium/augury"><button class="outline">Let the Augury Decide</button></form>""");
        html.Append("</div></div>");

        if (view.Calendar.Count == 0)
            html.Append("<p>No rite waits. Summon new rites, or the Machine Spirit writes tonight's on the spot.</p>");
        else
        {
            html.Append($"<p>{view.Calendar.Count} unconsecrated {(view.Calendar.Count == 1 ? "rite waits" : "rites wait")}. The Scribes' order comes first; the Augury orders the rest.</p>");
            html.Append("""<ol class="calendar">""");
            for (var index = 0; index < view.Calendar.Count; index++)
                CalendarEntry(html, view.Calendar[index], isFirst: index == 0, isLast: index == view.Calendar.Count - 1);
            html.Append("</ol>");
        }
        html.Append("</section>");

        html.Append("""<section aria-labelledby="ashes-heading"><h2 id="ashes-heading">The Ashes</h2>""");
        if (view.Ashes.Count == 0)
            html.Append("<p>Nothing has been consigned to the flames.</p>");
        else
        {
            html.Append("""<ul class="calendar">""");
            foreach (var draft in view.Ashes)
                Ash(html, draft);
            html.Append("</ul>");
        }
        html.Append("</section>");

        html.Append("""<section aria-labelledby="judgments-heading"><h2 id="judgments-heading">Recent judgments</h2>""");
        if (view.Decisions.Count == 0)
            html.Append("<p>The Scribes have not judged yet.</p>");
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
        html.Append("</section></main></body></html>");
        return html.ToString();
    }

    public static string RenderError()
    {
        var html = new StringBuilder();
        Head(html, "The Scriptorium is silent");
        html.Append("""
            <main id="main">
            <h1>The Scriptorium is silent</h1>
            <p class="lede">The Machine Spirit could not open the records. Try again in a moment.</p>
            <p><a href="/scriptorium">Return to the Scriptorium</a></p>
            </main></body></html>
            """);
        return html.ToString();
    }

    private static void CalendarEntry(StringBuilder html, CalendarEntry entry, bool isFirst, bool isLast)
    {
        var draft = entry.Draft;
        var id = draft.Id.ToString("D");
        html.Append($"""<li><article class="rite" aria-labelledby="title-{id}">""");
        html.Append($"""<p class="kicker"><time datetime="{entry.PublishesOnUtc:yyyy-MM-dd}">{Date(entry.PublishesOnUtc)}</time> · {Kind(draft.Kind)}""");
        if (entry.PlacedByScribes)
            html.Append(""" · <span class="placed">Placed by the Scribes</span>""");
        html.Append("</p>");
        RiteBody(html, draft, id);
        var quality = draft.Augury.Map(augury => $"The Augury: {augury.Quality:P0}, its place #{entry.AuguryPlace}").IfNone("Not yet judged by the Augury");
        html.Append($"""<p class="meta">{Encode(quality)} · written by <span class="nowrap">{Encode(draft.GeneratedByModel)}</span></p>""");

        DecisionForm(html, id, draft.Title,
        [
            .. isFirst ? Array.Empty<(string, string, string)>() : [("anoint", "Anoint", "secondary"), ("exalt", "Exalt", "secondary")],
            .. isLast ? Array.Empty<(string, string, string)>() : [("humble", "Humble", "secondary")],
            ("burn", "Consign to the Flames", "danger"),
        ]);
        html.Append("</article></li>");
    }

    private static void Ash(StringBuilder html, Draft draft)
    {
        var id = draft.Id.ToString("D");
        html.Append($"""<li><article class="rite ash" aria-labelledby="title-{id}">""");
        html.Append($"""<p class="kicker">{Kind(draft.Kind)}""");
        draft.BurnedAtUtc.IfSome(burned => html.Append($" · burned {Date(DateOnly.FromDateTime(burned))}"));
        html.Append("</p>");
        RiteBody(html, draft, id);
        DecisionForm(html, id, draft.Title, [("restore", "Restore from the Ashes", "secondary")]);
        html.Append("</article></li>");
    }

    private static void RiteBody(StringBuilder html, Draft draft, string id)
    {
        html.Append($"""<h3 id="title-{id}">{WithCode(draft.Title)}</h3>""");
        html.Append($"""<p class="text">{WithCode(draft.Text)}</p>""");
        html.Append($"""<p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(draft.HereticalTruth)}</p>""");
    }

    // One form per draft, so its note goes with whichever button is pressed. Each button names the rite for screen
    // readers, since every entry has the same buttons.
    private static void DecisionForm(StringBuilder html, string id, string title, IEnumerable<(string Action, string Label, string Style)> buttons)
    {
        html.Append($"""
            <form method="post" class="decide">
            <label for="note-{id}">Note for the Augury <span class="optional">(optional)</span></label>
            <textarea id="note-{id}" name="note" rows="2" maxlength="{ScribeDecision.MaxNoteLength}"></textarea>
            <div class="buttons">
            """);
        foreach (var (action, label, style) in buttons)
            html.Append($"""<button class="{style}" formaction="/scriptorium/drafts/{id}/{action}">{label}<span class="visually-hidden">: {Encode(title)}</span></button>""");
        html.Append("</div></form>");
    }

    private static void Head(StringBuilder html, string title)
    {
        html.Append($"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>{Encode(title)} · The Daily Machine Spirit</title>
            <link rel="preconnect" href="https://fonts.googleapis.com">
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
            <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Cinzel:wght@700&family=EB+Garamond:ital@0;1&family=Figtree:wght@400;600&family=JetBrains+Mono&display=swap">
            <style>{Styles}</style>
            </head>
            <body>
            <a class="skip" href="#main">Skip to content</a>
            <header><p class="brand">The Daily Machine Spirit</p></header>
            """);
    }

    private const string Styles = """
        :root { color-scheme: dark; }
        * { box-sizing: border-box; }
        body { margin: 0; background: #100e0b; color: #ede3cf; font: 16px/1.5 Figtree, system-ui, sans-serif; }
        header, main { max-width: 820px; margin: 0 auto; padding: 28px clamp(16px, 4vw, 40px) 0; }
        main { padding-bottom: 72px; }
        .brand { margin: 0; font: 700 16px Cinzel, serif; letter-spacing: .04em; color: #c9a25a; }
        h1 { font: 700 clamp(30px, 4.4vw, 46px)/1.1 Cinzel, serif; margin: 24px 0 8px; }
        h2 { font: 700 26px/1.2 Cinzel, serif; margin: 0; }
        h3 { font: 700 21px/1.2 Cinzel, serif; margin: 6px 0 8px; }
        .lede { font: italic 400 20px/1.4 "EB Garamond", serif; color: #cdbd9e; margin: 0 0 16px; }
        .status:not(:empty) { background: #1a1612; border-left: 4px solid #c9a25a; padding: 12px 16px; border-radius: 12px; }
        section { margin-top: 44px; }
        .section-head { display: flex; flex-wrap: wrap; gap: 12px 24px; align-items: center; justify-content: space-between; margin-bottom: 8px; }
        .actions, .buttons { display: flex; flex-wrap: wrap; gap: 8px; }
        .actions form { margin: 0; }
        .calendar, .judgments { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 16px; }
        .rite { background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; padding: 20px clamp(16px, 3vw, 24px); }
        .ash { opacity: .92; }
        .kicker { margin: 0; font: 700 13px Cinzel, serif; letter-spacing: .16em; text-transform: uppercase; color: #c9a25a; }
        .placed { color: #f0b45a; }
        .text { font: 400 19px/1.55 "EB Garamond", serif; margin: 0 0 12px; }
        .truth { background: #141c1f; border: 1px solid #2e4248; border-radius: 16px; padding: 12px 16px; color: #dbe6e8; margin: 0 0 12px; }
        .truth-label { display: block; font: 500 14px "JetBrains Mono", monospace; letter-spacing: .14em; text-transform: uppercase; color: #8cc4ae; }
        code { font-family: "JetBrains Mono", monospace; font-size: .9em; background: #0b1214; color: #f0d58c; padding: 1px 6px; border-radius: 6px; }
        .text code { background: #2b2117; color: #f0d58c; }
        .meta, .when, .optional { color: #b6a98f; }
        .meta { margin: 0 0 12px; font-size: 15px; }
        .decide label { display: block; font-weight: 600; margin-bottom: 4px; }
        textarea { width: 100%; background: #100e0b; color: #ede3cf; border: 1px solid rgba(237,227,207,.22); border-radius: 12px; padding: 8px 12px; font: 16px/1.4 Figtree, sans-serif; margin-bottom: 8px; }
        button { min-height: 46px; padding: 10px 18px; border-radius: 999px; font: 600 16px/1.2 Figtree, sans-serif; cursor: pointer; border: 1px solid transparent; }
        .primary { background: #c9a25a; color: #100e0b; }
        .primary:hover { background: #d6b170; }
        .outline, .secondary { background: transparent; color: #ede3cf; border-color: rgba(237,227,207,.22); }
        .outline:hover, .secondary:hover { background: rgba(237,227,207,.08); border-color: rgba(237,227,207,.4); }
        .danger { background: #3b1814; color: #f6cfc6; border-color: #d0604d; }
        .danger:hover { background: #4a1f19; }
        .judgments li { background: #1a1612; border-radius: 12px; padding: 10px 14px; }
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

    private static string Kind(RiteKind kind) => kind == RiteKind.Prayer ? "A Prayer" : "A Ritual";

    private static string Date(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    private static string Done(ScribeAction action) => action switch
    {
        ScribeAction.Anoint => "Anointed",
        ScribeAction.Exalt => "Exalted",
        ScribeAction.Humble => "Humbled",
        ScribeAction.Burn => "Consigned to the flames",
        _ => "Restored from the ashes",
    };

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The rites put commands and file names in `backticks`.
    private static string WithCode(string text) => InlineCode().Replace(Encode(text), "<code>$1</code>");

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCode();
}
