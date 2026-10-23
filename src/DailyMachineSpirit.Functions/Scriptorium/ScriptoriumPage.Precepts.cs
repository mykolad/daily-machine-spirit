using System.Globalization;
using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Pages;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

public static partial class ScriptoriumPage
{
    /// <summary>
    /// The Precepts (#12, the Scriptorium redesign's step 2): what the Machine Spirit learned from the Scribes' judgments,
    /// numbered, for the Scribes to pin, edit or delete (with an Undo), add to, and distil again. Plain form posts; the
    /// page's script edits in place and fades the Undo.
    /// </summary>
    /// <param name="outcome">What the last action did, if anything.</param>
    /// <param name="acted">The precept it acted on, marked so the eye finds it; after a delete, the one deleted.</param>
    /// <param name="counts">What each tab holds; none when it couldn't be counted.</param>
    /// <param name="trend">How far the Scribes agreed with the Augury, month by month, the newest first.</param>
    /// <param name="refused">A precept the check refused, kept in its field with why.</param>
    public static string RenderPrecepts(
        StoredPrecepts stored, Option<TabCounts> counts, IReadOnlyList<AgreementMonth> trend, Option<string> outcome, Option<Guid> acted,
        Option<RefusedPrecept> refused, PreceptsShown shown)
    {
        var book = stored.Book;
        // Sent back with each change to a precept: a page shown before another change is told to look again.
        var version = $"""<input type="hidden" name="etag" value="{Encode(stored.ETag.IfNone(string.Empty))}">""";
        // Straight after a delete, the Undo says it, so the message doesn't say it twice.
        var undoable = outcome.Exists(done => done == "delete")
            ? book.LastDeleted.Filter(deleted => acted.Exists(id => id == deleted.Precept.Id))
            : None;
        var html = new StringBuilder();
        Head(html, "The Precepts", TabBar(ScriptoriumTab.Precepts, counts), Identity(shown.Name));
        html.Append("""<main id="main">""");
        Heading(html, "The Precepts",
            "What the Machine Spirit has learned from the Scribes' judgments. It follows them in every rite it writes.");
        var status = refused.Map(precept => Encode($"Not saved: {precept.Reason} {WhereTheWordsAre(precept, book)}"))
            | (undoable.IsSome ? None : outcome.Map(PreceptStatus));
        html.Append($"""<p class="status" role="status" id="precepts-status">{status.IfNone(string.Empty)}</p>""");
        // Straight after the message, so the keyboard and a screen reader reach it first, and it stays until another
        // change, which ends it anyway: a time limit would take it away before some could reach it (WCAG 2.2.1).
        undoable.IfSome(deleted => html.Append($"""
            <div class="toasts"><div class="toast"><span class="toast-text">Precept deleted.</span><form method="post" action="/scriptorium/precepts/{deleted.Precept.Id:D}/restore">{version}<button class="undo" title="Puts the precept back where it was">{ScriptoriumIcons.Undo}Undo</button></form></div></div>
            """));
        DistilPanel(html, book, shown.NowUtc);

        if (book.Precepts.Count == 0)
        {
            html.Append("""<p class="empty">There are no precepts yet: distil them, or write one of your own.</p>""");
        }
        html.Append("""<ol class="precepts">""");
        // Just distilled, the new ones are the unpinned: marked, as an acted-on precept is, so the eye finds them.
        var distilled = outcome.Exists(done => done == "distil");
        for (var index = 0; index < book.Precepts.Count; index++)
        {
            var precept = book.Precepts[index];
            var mine = refused.Filter(found => found.PreceptId.Exists(id => id == precept.Id));
            PreceptEntry(html, precept, index + 1, version, distilled && !precept.Pinned || acted.Exists(id => id == precept.Id),
                shown.Editing.Exists(id => id == precept.Id) || mine.IsSome, mine, shown.Names);
        }
        OwnPrecept(html, book, refused.Filter(precept => precept.PreceptId.IsNone));
        html.Append("</ol>");
        Agreement(html, trend);
        html.Append("</main>");
        Footer(html, Some(SlowActionScript.Js + PreceptsScript.Js));
        return html.ToString();
    }

    // When they were distilled, from how much, and when they will be; and the way to distil them now.
    private static void DistilPanel(StringBuilder html, PreceptBook book, DateTime nowUtc)
    {
        var next = $"next: {Date(DateOnly.FromDateTime(NextDistillation(nowUtc)))}";
        var (when, about) = book.DistilledAtUtc.Match(
            Some: at => ($"Distilled {at.ToString("ddd d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture)}",
                $"{book.DistilledFrom.Map(count => $"From {count} judgments, and again").IfNone("Again")} every Monday ({next})."),
            None: () => ("Not distilled yet", $"They're distilled every Monday from the Scribes' judgments ({next})."));
        html.Append($"""
            <div class="distil-panel">
            <div class="distil-said"><p class="distilled-at">{Encode(when)}</p><p class="distil-about">{Encode(about)} Pinned precepts stay as they are; the others make way for the new ones.</p></div>
            <form method="post" action="/scriptorium/precepts/distil" class="distil-form">
            <button class="primary" aria-describedby="distil-hint" data-slow="{Encode(Distilling)}" data-busy-label="Distilling…">{ScriptoriumIcons.Flask}{ScriptoriumIcons.Loader}Distil the Precepts Now</button>
            <span class="hint" id="distil-hint">Usually seconds, at most a minute and a half.</span>
            </form>
            </div>
            """);
    }

    /// <summary>The weekly distillation's next run: Monday at 01:00 UTC (<c>DistilPrecepts</c>'s schedule).</summary>
    public static DateTime NextDistillation(DateTime nowUtc)
    {
        var monday = nowUtc.Date.AddDays(((int)DayOfWeek.Monday - (int)nowUtc.DayOfWeek + 7) % 7).AddHours(1);
        return monday > nowUtc ? monday : monday.AddDays(7);
    }

    // A precept, numbered as the Scribes speak of them ("precept IV"), with who made it what it is, and its three
    // actions; its words open to edit when asked (?edit=, or the script in place), or when the check refused an edit.
    // Each action names the precept for screen readers, since every entry has the same three.
    private static void PreceptEntry(
        StringBuilder html, Precept precept, int number, string version, bool acted, bool editing, Option<RefusedPrecept> refused,
        IReadOnlyList<ScribeName> names)
    {
        var id = precept.Id.ToString("D");
        var action = $"/scriptorium/precepts/{id}";
        var named = $": {Encode(precept.Text)}";
        var pinTitle = precept.Pinned
            ? "Unpin: the next distillation may replace it with a new one."
            : "Pin: distilling again keeps it, word for word. Pinned precepts come first.";
        html.Append($"""
            <li class="precept{(precept.Pinned ? " pinned" : string.Empty)}{(acted ? " acted" : string.Empty)}{(editing ? " editing" : string.Empty)}" id="precept-{id}">
            <span class="numeral" aria-hidden="true">{RomanNumeral.Of(number)}</span>
            <div class="precept-body">
            <div class="precept-shown">
            <p class="precept-text" title="Click to edit">{WithCode(precept.Text)}</p>
            <p class="precept-meta">{PreceptMeta(precept, names)}</p>
            </div>
            <form method="post" action="{action}/edit" class="precept-edit">
            {version}
            <label for="precept-{id}-text">Its words</label>
            <textarea id="precept-{id}-text" name="text" rows="3" maxlength="{Precept.MaxLength}" aria-describedby="precept-{id}-hint{(refused.IsSome ? " precepts-status" : string.Empty)}"{(refused.IsSome ? " aria-invalid=\"true\"" : string.Empty)} required>{Encode(refused.Map(found => found.Text).IfNone(precept.Text))}</textarea>
            <div class="buttons"><button class="primary">{ScriptoriumIcons.Save}Save<span class="visually-hidden">{named}</span></button><a class="cancel button-link" href="/scriptorium/precepts#precept-{id}">Cancel</a><span class="button-help" id="precept-{id}-hint">Saving pins it, so distilling again keeps your words.</span></div>
            </form>
            </div>
            <form method="post" class="precept-actions">
            {version}
            <button class="icon-button pin" formaction="{action}/{(precept.Pinned ? "unpin" : "pin")}" aria-pressed="{(precept.Pinned ? "true" : "false")}" title="{pinTitle}">{(precept.Pinned ? ScriptoriumIcons.PinFilled : ScriptoriumIcons.Pin)}<span class="visually-hidden">Pin, so distilling keeps it{named}</span></button>
            <a class="icon-button edit" href="/scriptorium/precepts?edit={id}#precept-{id}" title="Edit its words">{ScriptoriumIcons.Pencil}<span class="visually-hidden">Edit{named}</span></a>
            <button class="icon-button delete" formaction="{action}/delete" title="Delete it: the Machine Spirit won't propose it again">{ScriptoriumIcons.Delete}<span class="visually-hidden">Delete{named}</span></button>
            </form>
            </li>
            """);
    }

    // Whose words a precept is, and who pinned it: a pinned one in the Scribes' gold, with its pin.
    private static string PreceptMeta(Precept precept, IReadOnlyList<ScribeName> names)
    {
        var who = precept.By.Map(scribe => Encode(ScribeNames.Of(scribe, names))).IfNone("a Scribe");
        var pinned = precept.Pinned ? " · pinned" : string.Empty;
        var said = precept.Origin switch
        {
            PreceptOrigin.Written => $"Written by {who}{pinned}",
            PreceptOrigin.Amended => $"Amended by {who}{pinned}",
            PreceptOrigin.ScribesWords => $"A Scribe’s words{pinned}",
            _ => precept.Pinned ? $"Pinned by {who}" : "Distilled by the Machine Spirit",
        };
        return precept.Pinned ? $"""<span class="pinned-by">{ScriptoriumIcons.PinFilled}{said}</span>""" : said;
    }

    // The last of the list: a precept of the Scribe's own, kept pinned. Its button waits for words (the page's script).
    private static void OwnPrecept(StringBuilder html, PreceptBook book, Option<RefusedPrecept> refused)
    {
        if (book.Precepts.Count >= PreceptBook.MaxPrecepts)
        {
            return;
        }
        html.Append($"""
            <li class="precept own">
            <span class="numeral" aria-hidden="true">{RomanNumeral.Of(book.Precepts.Count + 1)}</span>
            <form method="post" action="/scriptorium/precepts/add" class="precept-add">
            <label for="precept-new">A precept of your own <span class="optional">(kept pinned)</span></label>
            <textarea id="precept-new" name="text" rows="2" maxlength="{Precept.MaxLength}" placeholder="Prefer rites a junior developer would recognise."{Refusal(refused)} required>{Encode(refused.Map(precept => precept.Text).IfNone(string.Empty))}</textarea>
            <div class="buttons"><button class="outline" data-needs-words>{ScriptoriumIcons.Add}Add the Precept</button></div>
            </form>
            </li>
            """);
    }

    // A refused precept's words go back in their field; when that field is gone (the precept was deleted or replaced
    // while the check ran, or the Precepts filled up), the message carries them instead, so they aren't lost, and an
    // edit doesn't quietly become an addition.
    private static string WhereTheWordsAre(RefusedPrecept refused, PreceptBook book)
    {
        var fieldShown = refused.PreceptId.Match(
            Some: id => book.Precepts.Exists(precept => precept.Id == id),
            None: () => book.Precepts.Count < PreceptBook.MaxPrecepts);
        if (fieldShown)
        {
            return refused.NextStep;
        }
        var gone = refused.PreceptId.IsSome
            ? "The precept changed meanwhile (another Scribe, or a distillation), so it isn't on the page any more."
            : "The Precepts filled up meanwhile, so there's no room for it now.";
        return $"{gone} Your words were: “{refused.Text}”";
    }

    // The new precept's field, when the check refused what was typed in it: marked invalid, and described by the status
    // that says why.
    private static string Refusal(Option<RefusedPrecept> refused)
        => refused.IsSome ? " aria-invalid=\"true\" aria-describedby=\"precepts-status\"" : string.Empty;

    // Knowing when the Augury is good enough: a table, so a screen reader can tell each figure's month and meaning.
    // Months in a row without a judgment share one row: they'd only repeat "no judgments".
    private static void Agreement(StringBuilder html, IReadOnlyList<AgreementMonth> trend)
    {
        html.Append($"""
            <section aria-labelledby="agreement-heading">
            <h2 id="agreement-heading">Do the Scribes Agree with the Augury?</h2>
            <p>How many of the Augury's top {AgreementTrend.TopPickPlaces} picks the Scribes burned, and how far they moved
            drafts from the places it gave them. When both stay low, the Augury can be left to choose on its own.</p>
            <div class="agreement-card" tabindex="0" role="region" aria-labelledby="agreement-heading">
            <table class="agreement">
            <thead><tr><th scope="col">Month</th><th scope="col" class="number">Judgments</th>
            <th scope="col" class="number">Top picks burned</th><th scope="col" class="number">Moved, on average</th></tr></thead>
            <tbody>
            """);
        for (var index = 0; index < trend.Count; index++)
        {
            var month = trend[index];
            if (month.ScribeDecisions == 0)
            {
                var oldest = index;
                while (oldest + 1 < trend.Count && trend[oldest + 1].ScribeDecisions == 0)
                {
                    oldest++;
                }
                html.Append($"""<tr class="quiet"><th scope="row">{MonthsBetween(trend[oldest].FirstDayUtc, month.FirstDayUtc)}</th><td colspan="3">No judgments</td></tr>""");
                index = oldest;
                continue;
            }
            var burned = month.DraftsBurned == 0
                ? """<span class="muted">No burns</span>"""
                : $"""{month.AuguryTopPicksBurned} <span class="muted">of {Count(month.DraftsBurned, "burn")}</span>""";
            var moved = month.AveragePlacesFromAuguryPlace.Match(
                Some: distance => $"""{Places(distance)} <span class="muted">({Count(month.JudgedDraftsMoved, "move")})</span>""",
                None: () => """<span class="muted">No moves</span>""");
            html.Append($"""<tr><th scope="row">{MonthName(month.FirstDayUtc)}</th>"""
                + $"""<td class="number">{month.ScribeDecisions}</td><td class="number">{burned}</td><td class="number">{moved}</td></tr>""");
        }
        html.Append("</tbody></table></div></section>");
    }

    // "May–Sep 2026", or "Dec 2025–Jan 2026" across a year, or one month alone.
    private static string MonthsBetween(DateOnly oldest, DateOnly newest)
        => oldest == newest ? MonthName(oldest)
            : oldest.Year == newest.Year ? $"{oldest.ToString("MMM", CultureInfo.InvariantCulture)}–{MonthName(newest)}"
            : $"{MonthName(oldest)}–{MonthName(newest)}";

    private static string MonthName(DateOnly firstDay) => firstDay.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string Places(double distance)
        => distance == 1 ? "1 place" : $"{distance.ToString("0.#", CultureInfo.InvariantCulture)} places";

    private static string PreceptStatus(string outcome) => Encode(outcome switch
    {
        "distil" => "The Precepts were distilled again from the Scribes' judgments.",
        "distil-nothing" => PreceptDistiller.NothingToLearn.Message,
        "distil-pinned" => PreceptDistiller.AllPinned.Message,
        "distil-refused" => PreceptDistiller.NothingWithinTheRules.Message,
        "distil-unchecked" => $"{PreceptDistiller.Unchecked.Message} Try again in a moment.",
        "distil-failed" => "The Machine Spirit couldn't distil them just now. Try again in a moment.",
        "add" => "Your precept was added, pinned.",
        "edit" => "The precept was changed and pinned: distilling again keeps it.",
        "pin" => "Pinned: distilling again keeps it.",
        "unpin" => "Unpinned: the next distillation may replace it.",
        "delete" => "Deleted. The Machine Spirit won't propose it again.",
        "restore" => "The precept is back where it was.",
        "text-needed" => PreceptKeeping.TextNeeded.Message,
        "too-long" => PreceptKeeping.TooLong.Message,
        "full" => PreceptKeeping.Full.Message,
        _ => "The Precepts changed meanwhile: another Scribe, or a distillation. Look again.",
    });
}
