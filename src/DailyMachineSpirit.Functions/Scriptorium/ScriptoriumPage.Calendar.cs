using System.Globalization;
using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Calendar (the Scriptorium redesign's step 1, <c>design_handoff_scriptorium_calendar</c>): the rites the Scribes
/// placed, then the Augury's, each a row that opens into its card.
/// </summary>
public static partial class ScriptoriumPage
{
    // Why a burn is asked for at the moment of burning: the first two are the Record's most common burn notes, the other
    // two the rubric's (#10) own measures, so the notes teach the Precepts in the words the judge scores by.
    private static readonly string[] BurnReasons = ["Too specific", "Duplicate", "The joke falls flat", "The truth is wrong"];

    /// <param name="shown">The subject the calendar is narrowed to, and whether every rite is open.</param>
    /// <param name="counts">What each tab holds; none when it couldn't be counted.</param>
    /// <param name="outcome">What the last action did (<see cref="ScriptoriumFunction"/>'s <c>done</c>), if anything.</param>
    /// <param name="rite">The draft it acted on, so the message can name it and say where it went.</param>
    /// <param name="burned">That draft, when the action burned it: it's in the Ashes now, not on this page.</param>
    /// <param name="name">How the Record shows the Scribe looking, with a way to choose or change it (#84); none when the
    /// names can't be read.</param>
    /// <param name="names">The names the Scribes chose, to say who placed a rite.</param>
    public static string Render(
        ScriptoriumView view, CalendarShown shown, Option<TabCounts> counts, Option<string> outcome, Option<Guid> rite, Option<Draft> burned,
        Option<NameInTheRecord> name, IReadOnlyList<ScribeName> names)
    {
        var status = outcome.Bind(done => Status(done, view, rite, burned));
        var html = new StringBuilder();
        Head(html, "The Scriptorium", TabBar(ScriptoriumTab.Calendar, counts), Identity(name));
        // Rites being summoned arrive one at a time over minutes: the script shows each as it comes, until the calendar is
        // full (data-summoning), by how many wait now (data-waiting).
        var full = view.Calendar.Count >= view.FullAt;
        var summoning = outcome.Exists(done => done is "summon" or "burn-last") && !full
            ? $" data-summoning=\"{view.FullAt}\""
            : string.Empty;
        html.Append($"""<main id="main" data-waiting="{view.Calendar.Count}"{summoning}{(shown.OpenAll ? " data-open-all" : string.Empty)}>""");
        Heading(html, "The Scriptorium",
            "Unconsecrated rites await the judgment of the Scribes. Your notes teach the Augury what to choose when no Scribe is watching.");
        html.Append($"""<p class="status" role="status">{status.IfNone(string.Empty)}</p>""");

        html.Append($"""
            <section aria-labelledby="calendar-heading">
            <div class="section-head">
            <h2 id="calendar-heading">The Liturgical Calendar</h2>
            <div class="actions">
            <form method="post" action="/scriptorium/summon">{SubjectField(shown.Subject)}{SummonButton(view, full)}</form>
            """);
        if (view.Calendar.Any(entry => entry.PlacedByScribes))
        {
            html.Append($"""<form method="post" action="/scriptorium/augury">{SubjectField(shown.Subject)}<button title="{Encode(About("augury"))}" class="outline">{ScriptoriumIcons.Augury}Let the Augury Decide</button></form>""");
        }
        html.Append("</div></div>");

        if (view.Calendar.Count == 0)
        {
            html.Append("<p>No rite waits. Summon new rites, or the Machine Spirit writes tonight's on the spot.</p>");
        }
        else
        {
            Calendar(html, view, shown.Subject, rite, names);
        }
        html.Append("</section>");
        RecentJudgments(html, view.Decisions);
        html.Append("</main>");
        UndoToast(html);
        Footer(html, Some(ScriptoriumScript.Js));
        return html.ToString();
    }

    // Who's looking, and the pages under their name rather than a tab. Not the tab bar's: the Expurgator's Desk is only
    // the Expurgators' (their own Access application), and a name is chosen once.
    private static string Identity(Option<NameInTheRecord> name)
    {
        var signedIn = name.Map(yours =>
            $"""<span>Signed in as <strong>{Encode(yours.Shown)}</strong></span><a href="/scriptorium/name">{(yours.Chosen ? "Change your name" : "Choose a name")}</a><span aria-hidden="true">·</span>""")
            .IfNone(string.Empty);
        return $"""<p class="identity">{signedIn}<a href="/scriptorium/expurgate" title="Erasing words from published rites, for the Expurgators">The Expurgator’s Desk</a></p>""";
    }

    private static void Calendar(StringBuilder html, ScriptoriumView view, Option<Tag> subject, Option<Guid> acted, IReadOnlyList<ScribeName> names)
    {
        var calendar = view.Calendar;
        // No summary or help above the rites: the tab counts them, each row says its day, and each button says what it
        // does where the pointer rests.
        html.Append("""<div class="filters">""");
        Subjects(html, calendar, subject);
        html.Append(EveryRite());
        html.Append("</div>");

        var anointed = calendar.Count(entry => entry.PlacedByScribes);
        var rows = calendar.Select((entry, index) => new CalendarRow(entry, index + 1, index + 1 == anointed)).ToList();
        Group(html, rows.Where(row => row.Entry.PlacedByScribes).ToList(), subject, view.NowUtc, acted, names, new(
            "scribes", ScriptoriumIcons.Anoint, "Anointed", "Published in this order, before the Augury’s.",
            "No rite is anointed yet.", "among the anointed"));
        Group(html, rows.Where(row => !row.Entry.PlacedByScribes).ToList(), subject, view.NowUtc, acted, names, new(
            "augury", ScriptoriumIcons.Augury, "The Augury’s order", "Ordered by quality and variety. Anoint one to give it a place.",
            "Every rite is anointed.", "in the Augury’s order"));
    }

    // What the waiting rites are about, by their tags, most first, so the Scribes see a calendar heavy on one subject;
    // each narrows the calendar to its rites. Links, so the filter works without the script, which loads them in place.
    private static void Subjects(StringBuilder html, IReadOnlyList<CalendarEntry> calendar, Option<Tag> subject)
    {
        var counted = calendar
            .SelectMany(entry => RiteTags.Of(entry.Draft.Tags))
            .GroupBy(tag => tag)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Label, StringComparer.Ordinal)
            .Select(group => (Tag: group.Key, Count: group.Count()))
            .ToList();
        // The subject chosen stays offered when its last rite has gone, so the Scribe sees what the calendar is narrowed to.
        subject.Filter(tag => !counted.Exists(found => found.Tag == tag)).IfSome(tag => counted.Add((tag, 0)));

        html.Append("""<p class="subjects" role="group" aria-label="Filter by subject">""");
        html.Append(Chip("/scriptorium", "All", calendar.Count, subject.IsNone));
        foreach (var (tag, count) in counted)
        {
            var current = subject.Exists(chosen => chosen == tag);
            // The chosen subject's chip clears the filter.
            html.Append(Chip(current ? "/scriptorium" : $"/scriptorium?subject={tag.Key}", tag.Label, count, current));
        }
        html.Append("</p>");
    }

    // Opening or folding every rite at once is the script's: without it, every rite is open, so the pair stays hidden.
    // Two buttons rather than one that changes its name, so neither needs a state to be understood.
    private static string EveryRite()
        => $"""<span class="pair every" role="group" aria-label="Every rite" hidden><button type="button" class="step" data-fold="open" title="Open every rite shown">{ScriptoriumIcons.OpenEvery}<span class="visually-hidden">Open every rite</span></button><button type="button" class="step" data-fold="fold" title="Fold every rite but the next">{ScriptoriumIcons.FoldEvery}<span class="visually-hidden">Fold every rite</span></button></span>""";

    private static string Chip(string href, string label, int count, bool current)
        => $"""<a class="chip" href="{href}"{(current ? " aria-current=\"true\"" : string.Empty)}>{Encode(label)}<span class="chip-count">{count}</span></a>""";

    // A refill writes only what's missing, so a full calendar has nothing to summon, and says why where the pointer rests.
    private static string SummonButton(ScriptoriumView view, bool full)
        => full
            ? $"""<button title="The calendar is full: {view.FullAt} rites wait. More are written as rites are published or burned." class="primary" disabled>{ScriptoriumIcons.Summon}Summon New Rites</button>"""
            : $"""<button title="{Encode(About("summon"))}" class="primary">{ScriptoriumIcons.Summon}Summon New Rites</button>""";

    // Each of the calendar's forms carries the subject it's narrowed to, so the calendar comes back narrowed.
    private static string SubjectField(Option<Tag> subject)
        => subject.Map(tag => $"""<input type="hidden" name="subject" value="{tag.Key}">""").IfNone(string.Empty);

    // One of the calendar's two orders. Narrowed to a subject, it shows that subject's rites, each in its own place.
    private static void Group(
        StringBuilder html, List<CalendarRow> rows, Option<Tag> subject, DateTime nowUtc, Option<Guid> acted, IReadOnlyList<ScribeName> names,
        CalendarGroup group)
    {
        var shown = rows.Where(row => subject.ForAll(tag => row.Entry.Draft.Tags.Exists(keys => keys.Contains(tag.Key)))).ToList();
        var count = subject.IsSome ? $"{shown.Count} of {rows.Count}" : $"{rows.Count}";
        html.Append($"""
            <div class="group {group.Kind}-group">
            <div class="group-head"><h3 class="group-title" id="{group.Kind}-heading">{group.Icon}{Encode(group.Title)}<span class="group-count">{count}</span></h3><p class="group-hint">{Encode(group.Hint)}</p></div>
            """);
        if (shown.Count == 0)
        {
            var empty = subject.Map(tag => $"No {tag.Label} rites {group.Where}.").IfNone(group.Empty);
            html.Append($"""<p class="empty">{Encode(empty)}</p>""");
        }
        else
        {
            html.Append($"""<ul class="rites" aria-labelledby="{group.Kind}-heading">""");
            foreach (var row in shown)
            {
                Row(html, row, subject, nowUtc, acted, names);
            }
            html.Append("</ul>");
        }
        html.Append("</div>");
    }

    // A rite as a row: its place, day, title and tags, the Augury's score, and for an anointed one Exalt and Humble, then
    // its card. The card shows without the script, which folds every card but the next to publish; the chevron opens it
    // again. The rite the last action was on is marked, so the Scribe's eye finds where it went.
    private static void Row(
        StringBuilder html, CalendarRow row, Option<Tag> subject, DateTime nowUtc, Option<Guid> acted, IReadOnlyList<ScribeName> names)
    {
        var (entry, place, lastAnointed) = row;
        var draft = entry.Draft;
        var id = draft.Id.ToString("D");
        var title = draft.Writing.Title;
        var classes = $"rite row{(entry.PlacedByScribes ? " scribes" : string.Empty)}{(place == 1 ? " next" : string.Empty)}{Acted(draft, acted)}";
        html.Append($"""<li><article class="{classes}" id="rite-{id}" aria-labelledby="title-{id}">""");
        if (place == 1)
        {
            NextToPublish(html, entry.PublishesOnUtc, nowUtc);
        }

        // One form per rite, so its note goes with whichever button is pressed, Exalt and Humble's in the row too.
        html.Append("""<form method="post" class="decide">""");
        html.Append(SubjectField(subject));
        html.Append($"""
            <div class="row-head">
            <p class="where"><span class="place">#{place}</span><span class="day"><time datetime="{entry.PublishesOnUtc:yyyy-MM-dd}">{Date(entry.PublishesOnUtc)}</time><span class="kind">{(draft.Writing.Kind == RiteKind.Prayer ? "Prayer" : "Ritual")}</span></span></p>
            <div class="heading"><h4 id="title-{id}">{WithCode(title)}</h4>{Tags(draft)}</div>
            <div class="cluster">
            {Score(draft)}
            {(entry.PlacedByScribes ? $"""<span class="pair" role="group" aria-label="Reorder">{Exalt(id, title, place)}{Humble(id, title, lastAnointed)}</span>""" : string.Empty)}
            <button type="button" class="expand" aria-expanded="true" aria-controls="body-{id}" aria-label="Collapse {Encode(title)}" title="Show or fold away the whole rite" hidden>{ScriptoriumIcons.Chevron}</button>
            </div>
            </div>
            <div class="body" id="body-{id}">
            <p class="text">{WithCode(draft.Writing.Text)}</p>
            <p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(draft.Writing.HereticalTruth)}</p>
            {Ailment(draft.Writing)}
            <p class="meta">{Meta(entry, place, names)}</p>
            """);
        // Written again and again, it still repeated a rite: the Scribes decide whether it's new enough.
        draft.Resembles.IfSome(resembled => html.Append($"""<p class="resembles">Much like “{WithCode(resembled)}”, already written.</p>"""));
        html.Append($"""
            <details class="note-fold">
            <summary>{ScriptoriumIcons.Add}Add a note for the Augury</summary>
            <label for="note-{id}">Note for the Augury <span class="optional">(sent with your next judgment)</span></label>
            <textarea id="note-{id}" name="note" rows="2" maxlength="{ScribeDecision.MaxNoteLength}"></textarea>
            </details>
            """);
        html.Append($"""<div class="buttons">{string.Concat(
            // Anointing the Augury's first is useful too: it keeps it there, ahead of a better draft a refill may bring.
            entry.PlacedByScribes ? string.Empty : Button(id, title, "anoint", ScriptoriumIcons.Anoint, "Anoint", "primary"),
            AmendLink(id, title, subject),
            // Only where judging couldn't score it: judging a judged draft again would change nothing its words did.
            draft.Augury.IsSome ? string.Empty : Button(id, title, "judge", ScriptoriumIcons.Augury, "Judge Again", "secondary"),
            entry.PlacedByScribes ? Button(id, title, "release", ScriptoriumIcons.Release, "Release to the Augury", "secondary") : string.Empty,
            Button(id, title, "burn", ScriptoriumIcons.Burn, "Consign to the Flames", "danger burn"))}</div>""");
        BurnPanel(html, id, title);
        html.Append("</div></form></article></li>");
    }

    // The rite the midnight run publishes, and when. Today's own is only still due when that run failed: its retry
    // publishes it.
    private static void NextToPublish(StringBuilder html, DateOnly day, DateTime nowUtc)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var when = midnight > nowUtc
            ? $"{Date(day)}, 00:00 UTC · in {Countdown(midnight - nowUtc)}"
            : $"{Date(day)} · publishes next";
        html.Append($"""<p class="next-head"><span class="next-label">Next to publish</span> <span class="next-when">{when}</span></p>""");
    }

    // Hours and minutes, the minutes rounded up, so the last minute still says "1m".
    private static string Countdown(TimeSpan left)
    {
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m";
    }

    // Under the title, where an eye matches them against the subjects above. Not links: following one would leave the
    // Scriptorium for the public tag page.
    private static string Tags(Draft draft)
    {
        var tags = RiteTags.Of(draft.Tags);
        return tags.Count == 0
            ? string.Empty
            : $"""<p class="tags"><span class="visually-hidden">Filed under </span>{string.Concat(tags.Select(tag => $"""<span class="tag">{Encode(tag.Label)}</span>"""))}</p>""";
    }

    private static string Score(Draft draft)
        => $"""<span class="score" title="How sure the Augury is of this rite"><span class="score-label">Augury</span> {draft.Augury.Match(
            Some: judged => $"""<span class="score-value">{Percent(judged.Quality)}</span>""",
            None: () => """<span class="score-value" aria-hidden="true">—</span><span class="visually-hidden">not yet judged</span>""")}</span>""";

    // Only the anointed are moved by hand (Scribes.Exalt, .Humble), among themselves: the Augury orders the rest. At the
    // ends of the anointed there's nowhere to go, so the button is there but can't be pressed.
    private static string Exalt(string id, string title, int place)
        => place == 1
            ? Step(ScriptoriumIcons.Exalt, "Exalt", title, "Already next to publish", None)
            : Step(ScriptoriumIcons.Exalt, "Exalt", title, "Exalt: one day earlier", Some($"/scriptorium/drafts/{id}/exalt"));

    private static string Humble(string id, string title, bool lastAnointed)
        => lastAnointed
            ? Step(ScriptoriumIcons.Humble, "Humble", title, "Already the last anointed: the Augury’s order follows", None)
            : Step(ScriptoriumIcons.Humble, "Humble", title, "Humble: one day later", Some($"/scriptorium/drafts/{id}/humble"));

    // An arrow alone; its name, for screen readers, says which rite, since every row has the same two.
    private static string Step(string icon, string verb, string title, string does, Option<string> action)
        => $"""<button class="step" title="{Encode(does)}"{action.Match(Some: path => $" formaction=\"{path}\"", None: () => " disabled")}>{icon}<span class="visually-hidden">{verb}: {Encode(title)}</span></button>""";

    // The Augury's own rank for a rite only says something where the Scribes put it elsewhere. Who placed it is said only
    // when known: the group already says it's the Scribes'.
    private static string Meta(CalendarEntry entry, int place, IReadOnlyList<ScribeName> names)
    {
        var draft = entry.Draft;
        var id = draft.Id.ToString("D");
        var parts = new List<string>
        {
            draft.Augury.Map(judged => $"Augury {Percent(judged.Quality)}").IfNone("Not yet judged by the Augury"),
        };
        if (entry.PlacedByScribes && draft.Augury.IsSome && entry.AuguryPlace != place)
        {
            parts.Add($"would rank it #{entry.AuguryPlace}");
        }
        if (entry.PlacedByScribes)
        {
            entry.PlacedBy.IfSome(scribe => parts.Add($"anointed by {Encode(ScribeNames.Of(scribe, names))}"));
        }
        parts.Add($"""written by <span class="nowrap">{Encode(draft.Writing.GeneratedByModel)}</span>{(draft.Writing.Edited ? ", amended by a Scribe" : string.Empty)}""");
        parts.Add($"""<a href="/scriptorium/record?draft={id}">its record</a>""");
        return string.Join(" · ", parts);
    }

    // Asked at the moment of burning, so the Augury learns why. Only the script shows it, in place of the note and the
    // buttons; without it, Consign to the Flames burns at once, with the note.
    private static void BurnPanel(StringBuilder html, string id, string title)
    {
        var reasons = string.Concat(BurnReasons.Select(reason => $"""<button type="button" class="reason" aria-pressed="false">{Encode(reason)}</button>"""));
        html.Append($"""
            <div class="burn-panel" role="group" aria-labelledby="why-{id}" hidden>
            <p class="why" id="why-{id}">Why consign it to the flames?</p>
            <p class="reasons">{reasons}</p>
            <label class="visually-hidden" for="reason-{id}">Why, in your own words</label>
            <textarea id="reason-{id}" class="reason-text" rows="2" maxlength="{ScribeDecision.MaxNoteLength}" placeholder="Or in your own words (optional)"></textarea>
            <div class="burn-buttons">
            <button title="{Encode(About("burn"))}" class="danger" formaction="/scriptorium/drafts/{id}/burn" data-confirm>{ScriptoriumIcons.Burn}Consign to the Flames<span class="visually-hidden">: {Encode(title)}</span></button>
            <button type="button" class="cancel">Cancel</button>
            <span class="burn-help">The reason teaches the Augury. A burned rite can be restored from the Ashes.</span>
            </div>
            </div>
            """);
    }

    // The newest judgments, with their notes, and the way to the rest.
    private static void RecentJudgments(StringBuilder html, IReadOnlyList<ScribeDecision> decisions)
    {
        html.Append("""
            <section aria-labelledby="judgments-heading">
            <div class="section-head"><h2 id="judgments-heading">Recent judgments</h2><a class="more" href="/scriptorium/record">The full Record<span aria-hidden="true"> →</span></a></div>
            """);
        if (decisions.Count == 0)
        {
            html.Append("<p>The Scribes have not judged yet.</p>");
        }
        else
        {
            html.Append("""<ul class="recent">""");
            foreach (var decision in decisions)
            {
                var day = DateOnly.FromDateTime(decision.DecidedAtUtc);
                html.Append($"""<li>{JudgmentIcon(decision.Action)}<time datetime="{day:yyyy-MM-dd}">{Date(day)}</time><p><span class="verb">{Encode(Done(decision.Action))}:</span> <cite>{WithCode(decision.Title)}</cite>""");
                decision.Note.IfSome(note => html.Append($"""<span class="note">“{Encode(note)}”</span>"""));
                html.Append("</p></li>");
            }
            html.Append("</ul>");
        }
        html.Append("</section>");
    }

    private static string JudgmentIcon(ScribeAction action) => action switch
    {
        ScribeAction.Burn => $"""<span class="judgment-icon burned">{ScriptoriumIcons.Burn}</span>""",
        ScribeAction.Anoint => $"""<span class="judgment-icon anointed">{ScriptoriumIcons.Anoint}</span>""",
        ScribeAction.Exalt => $"""<span class="judgment-icon">{ScriptoriumIcons.Exalt}</span>""",
        ScribeAction.Humble => $"""<span class="judgment-icon">{ScriptoriumIcons.Humble}</span>""",
        ScribeAction.Amend => $"""<span class="judgment-icon">{ScriptoriumIcons.Note}</span>""",
        ScribeAction.Restore => $"""<span class="judgment-icon">{ScriptoriumIcons.Restore}</span>""",
        _ => $"""<span class="judgment-icon">{ScriptoriumIcons.Release}</span>""",
    };

    // After a burn, the script offers to undo it for a few seconds: the rite goes back where it was. Filled from the
    // template into a live region that's always there, so a screen reader announces it.
    private static void UndoToast(StringBuilder html)
        => html.Append($"""
            <div class="toasts" id="toasts" role="status"></div>
            <template id="undo-toast"><div class="toast"><span class="toast-text"></span><form method="post"><button class="undo" title="Restores the rite from the Ashes, into the Augury's order">{ScriptoriumIcons.Undo}Undo</button></form></div></template>
            """);

    private static string Percent(float quality) => quality.ToString("0%", CultureInfo.InvariantCulture);

    private sealed record CalendarRow(CalendarEntry Entry, int Place, bool LastAnointed);

    /// <param name="Where">Where the group's rites are, for "No Caches rites …" when none of the subject's are.</param>
    private sealed record CalendarGroup(string Kind, string Icon, string Title, string Hint, string Empty, string Where);
}
