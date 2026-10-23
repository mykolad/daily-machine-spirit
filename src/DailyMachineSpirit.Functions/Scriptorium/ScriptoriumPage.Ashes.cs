using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Ashes (the Scriptorium redesign's step 2, <c>design_handoff_scriptorium_calendar</c>): the burned rites, by the
/// day they burned, newest first, each a row that opens into its card, with who burned it, from where and why.
/// </summary>
public static partial class ScriptoriumPage
{
    /// <param name="ashes">The rites burned most recently, the newest first.</param>
    /// <param name="subject">The subject the Ashes are narrowed to.</param>
    /// <param name="outcome">What the last action did here (<see cref="ScriptoriumFunction"/>'s <c>done</c>), if anything.</param>
    /// <param name="rite">The draft an address names: the one acted on, or one a burn's message linked to.</param>
    /// <param name="named">That draft, when it isn't among <paramref name="ashes"/>: burned longer ago, which the page
    /// shows too, or just restored, which the message names.</param>
    /// <param name="name">How the Record shows the Scribe looking; none when the names can't be read.</param>
    /// <param name="names">The names the Scribes chose, to say who burned a rite.</param>
    public static string RenderAshes(
        IReadOnlyList<Draft> ashes, Option<Tag> subject, Option<TabCounts> counts, Option<string> outcome, Option<Guid> rite, Option<Draft> named,
        Option<NameInTheRecord> name, IReadOnlyList<ScribeName> names)
    {
        var mentioned = rite.Bind(id => ashes.Where(ash => ash.Id == id).HeadOrNone()) | named;
        var status = outcome.Bind(done => AshesStatus(done, mentioned));
        var html = new StringBuilder();
        Head(html, "The Ashes", TabBar(ScriptoriumTab.Ashes, counts), Identity(name));
        html.Append("""<main id="main">""");
        Heading(html, "The Ashes",
            "Rites consigned to the flames. Nothing burned is lost: a restored rite waits again, where the Augury places it.");
        html.Append($"""<p class="status" role="status">{status.IfNone(string.Empty)}</p>""");
        html.Append("""<div class="filters">""");
        Subjects(html, ashes, subject, "/scriptorium/ashes");
        html.Append("</div>");

        var shown = ashes.Where(ash => InSubject(ash, subject)).ToList();
        var older = named.Filter(draft => draft.State == DraftState.Burned).ToSeq().ToList();
        html.Append("<div data-rows>");
        if (shown.Count == 0)
        {
            var empty = subject.Map(tag => $"No {tag.Label} rites in the Ashes.").IfNone("The Ashes are cold. Nothing has been burned.");
            html.Append($"""<p class="empty">{Encode(empty)}</p>""");
        }
        foreach (var day in shown.GroupBy(ash => ash.BurnedAtUtc.Map(DateOnly.FromDateTime)))
        {
            BurnDay(html, day.Key.Map(burned => $"Burned {Date(burned)}").IfNone("Burned long ago"),
                day.Key.Map(burned => $"burned-{burned:yyyy-MM-dd}").IfNone("burned-long-ago"), day.ToList(), subject, rite, names);
        }
        // A burn's message links to its rite, which newer burns may have pushed past the ones shown.
        if (older.Count > 0)
        {
            BurnDay(html, "Burned longer ago", "burned-longer-ago", older, subject, rite, names);
        }
        html.Append("</div>");
        // The page shows the newest; the tab counts them all.
        counts.Filter(counted => counted.Ashes > ashes.Count)
            .IfSome(counted => html.Append($"""<p class="group-hint more-ashes">The {ashes.Count} burned most recently, of {Number(counted.Ashes)}.</p>"""));
        html.Append("</main>");
        UndoToast(html, "Consigns it to the flames again, with the reason it had",
            """<input type="hidden" name="page" value="ashes"><input type="hidden" name="note">""");
        Footer(html, Some(ScriptoriumScript.Js));
        return html.ToString();
    }

    private static bool InSubject(Draft draft, Option<Tag> subject)
        => subject.ForAll(tag => draft.Tags.Exists(keys => keys.Contains(tag.Key)));

    private static void BurnDay(
        StringBuilder html, string title, string headingId, List<Draft> ashes, Option<Tag> subject, Option<Guid> acted, IReadOnlyList<ScribeName> names)
    {
        html.Append($"""
            <section class="group burn-day" aria-labelledby="{headingId}">
            <div class="group-head"><h2 class="group-title" id="{headingId}">{ScriptoriumIcons.Burn}{Encode(title)}<span class="group-count">{ashes.Count}</span></h2></div>
            <ul class="rites" aria-labelledby="{headingId}">
            """);
        foreach (var ash in ashes)
        {
            AshRow(html, ash, subject, acted, names);
        }
        html.Append("</ul></section>");
    }

    // A burned rite as a row: its kind and the place it burned from, its title, tags and why it burned, and Restore,
    // then its card. Every card shows without the script, which folds them; the chevron or the row opens one again.
    // The reason rides along for the script's Undo, which burns the rite again for the same reason.
    private static void AshRow(StringBuilder html, Draft draft, Option<Tag> subject, Option<Guid> acted, IReadOnlyList<ScribeName> names)
    {
        var id = draft.Id.ToString("D");
        var title = draft.Writing.Title;
        var from = draft.BurnedFromPlace.Map(place => $"""<span class="from">from #{place}</span>""").IfNone(string.Empty);
        var reason = draft.BurnReason.Map(why => $"""<span class="reason-said"><span class="visually-hidden">Why it burned: </span>“{Encode(why)}”</span>""")
            .IfNone(string.Empty);
        html.Append($"""
            <li><article class="rite row ash{Acted(draft, acted)}" id="rite-{id}" aria-labelledby="title-{id}" data-reason="{Encode(draft.BurnReason.IfNone(string.Empty))}">
            <form method="post" class="decide"><input type="hidden" name="page" value="ashes">{SubjectField(subject)}
            <div class="row-head">
            <p class="where"><span class="day ash-day"><span class="kind">{(draft.Writing.Kind == RiteKind.Prayer ? "Prayer" : "Ritual")}</span>{from}</span></p>
            <div class="heading"><h3 class="row-title" id="title-{id}">{WithCode(title)}</h3><div class="under-title">{Tags(draft)}{reason}</div></div>
            <div class="cluster">
            <button title="{Encode(About("restore"))}" class="outline restore-now" formaction="/scriptorium/drafts/{id}/restore">{ScriptoriumIcons.Restore}Restore<span class="visually-hidden"> from the Ashes: {Encode(title)}</span></button>
            <button type="button" class="expand" aria-expanded="true" aria-controls="body-{id}" aria-label="Collapse {Encode(title)}" title="Show or fold away the whole rite" hidden>{ScriptoriumIcons.Chevron}</button>
            </div>
            </div>
            <div class="body" id="body-{id}">
            <p class="text">{WithCode(draft.Writing.Text)}</p>
            <p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(draft.Writing.HereticalTruth)}</p>
            {Ailment(draft.Writing)}
            <p class="meta">{BurnMeta(draft, names)}</p>
            <details class="note-fold">
            <summary>{ScriptoriumIcons.Add}Add a note for the Augury</summary>
            <label for="note-{id}">Note for the Augury <span class="optional">(sent when you restore it)</span></label>
            <textarea id="note-{id}" name="note" rows="2" maxlength="{ScribeDecision.MaxNoteLength}"></textarea>
            </details>
            <div class="buttons">{Button(id, title, "restore", ScriptoriumIcons.Restore, "Restore from the Ashes", "primary")}<span class="button-help">It returns to the Augury’s order, where the Augury places it.</span></div>
            </div></form></article></li>
            """);
    }

    // Who burned it, as the Record has it; a burn older than the Record says "a Scribe". The place and the reason are in
    // the row, so only the reason's absence is said here.
    private static string BurnMeta(Draft draft, IReadOnlyList<ScribeName> names)
    {
        var by = draft.BurnedBy.Map(scribe => Encode(ScribeNames.Of(scribe, names))).IfNone("a Scribe");
        var parts = new List<string> { $"Burned by {by}" };
        if (draft.BurnReason.IsNone)
        {
            parts.Add("no reason given");
        }
        parts.Add($"""<a href="/scriptorium/record?draft={draft.Id:D}">its record</a>""");
        return string.Join(" · ", parts);
    }

    // What the last action here did. A restore names the rite and the way to it in the calendar, which the script's Undo
    // says instead (data-undoable); the Undo burns it again, back into the Ashes.
    private static Option<string> AshesStatus(string outcome, Option<Draft> rite)
    {
        var title = rite.Map(draft => $"“{WithCode(draft.Writing.Title)}”").IfNone("The rite");
        var calendar = rite.Map(draft => $""" <a href="/scriptorium?rite={draft.Id:D}#rite-{draft.Id:D}">See it in the calendar</a>.""").IfNone(string.Empty);
        return outcome switch
        {
            "restore" => $"""<span data-undoable>{title} returned to the Augury’s order.{calendar}</span>""",
            "burn" => $"{title} is back in the Ashes.",
            "burn-last" => $"{title} is back in the Ashes. No rite waits now, so new ones are being summoned: they're written over the next few minutes.",
            "changed" => "Another Scribe restored or burned it meanwhile. Look again before deciding.",
            "note-too-long" => "That note is too long, so nothing was changed. Shorten it and try again.",
            _ => None,
        };
    }
}
