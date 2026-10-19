using System.Globalization;
using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Precepts;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

public static partial class ScriptoriumPage
{
    /// <summary>
    /// The Precepts (#12): what the Machine Spirit learned from the Scribes' judgments, for the Scribes to edit, pin or
    /// delete, and to distil again. Plain form posts, without the calendar's script.
    /// </summary>
    /// <param name="outcome">What the last action did, if anything.</param>
    /// <param name="acted">The precept it acted on, marked so the eye finds it.</param>
    /// <param name="counts">What each tab holds; none when it couldn't be counted.</param>
    /// <param name="trend">How far the Scribes agreed with the Augury, month by month, the newest first.</param>
    /// <param name="refused">A precept the check refused, kept in its field with why.</param>
    public static string RenderPrecepts(
        StoredPrecepts stored, Option<TabCounts> counts, IReadOnlyList<AgreementMonth> trend, Option<string> outcome, Option<Guid> acted, Option<RefusedPrecept> refused)
    {
        var book = stored.Book;
        // Sent back with each change to a precept: a page shown before another change is told to look again.
        var version = $"""<input type="hidden" name="etag" value="{Encode(stored.ETag.IfNone(string.Empty))}">""";
        var html = new StringBuilder();
        Head(html, "The Precepts", TabBar(ScriptoriumTab.Precepts, counts));
        html.Append("""<main id="main">""");
        Heading(html, "The Precepts",
            "What the Machine Spirit has learned from the Scribes' judgments. It follows them in every rite it writes.");
        var status = refused.Map(precept => Encode($"Not saved: {precept.Reason} {WhereTheWordsAre(precept, book)}"))
            | outcome.Map(PreceptStatus);
        html.Append($"""<p class="status" role="status" id="precepts-status">{status.IfNone(string.Empty)}</p>""");

        html.Append($"""
            <p>{Encode(Distilled(book))} Pinned precepts stay as they are; the others make way for the new ones.</p>
            <form method="post" action="/scriptorium/precepts/distil" class="actions">
            <button class="primary" aria-describedby="distil-hint" data-slow="{Encode(Distilling)}" data-busy-label="Distilling…">{ScriptoriumIcons.Summon}Distil the Precepts Now</button>
            <span class="hint" id="distil-hint">Usually seconds, at most a minute and a half.</span>
            </form>
            """);

        if (book.Precepts.Count == 0)
        {
            html.Append("<p>There are no precepts yet.</p>");
        }
        else
        {
            html.Append("""<ol class="calendar">""");
            // Just distilled, the new ones are the unpinned: marked, as an acted-on precept is, so the eye finds them.
            var distilled = outcome.Exists(done => done == "distil");
            foreach (var precept in book.Precepts)
            {
                PreceptEntry(html, precept, version, distilled && !precept.Pinned ? Some(precept.Id) : acted,
                    refused.Filter(found => found.PreceptId.Exists(id => id == precept.Id)));
            }
            html.Append("</ol>");
        }

        var newRefused = refused.Filter(precept => precept.PreceptId.IsNone);
        if (book.Precepts.Count < PreceptBook.MaxPrecepts)
        {
            html.Append($"""
                <form method="post" action="/scriptorium/precepts/add" class="amend">
                <label for="precept-new">A precept of your own</label>
                <textarea id="precept-new" name="text" rows="2" maxlength="{Precept.MaxLength}"{Refusal(newRefused)} required>{Encode(newRefused.Map(precept => precept.Text).IfNone(string.Empty))}</textarea>
                <div class="buttons"><button class="secondary">{ScriptoriumIcons.Add}Add the Precept</button></div>
                </form>
                """);
        }
        Agreement(html, trend);
        html.Append("</main>");
        Footer(html, Some(SlowActionScript.Js));
        return html.ToString();
    }

    // Each precept's buttons name it for screen readers, since every entry has the same buttons. Pin and Unpin say what
    // they do in their title, as the calendar's buttons do: pinning only matters at the next distillation.
    /// <param name="refused">This precept's edit, if the check refused it: the fold opens on the Scribe's words.</param>
    private static void PreceptEntry(StringBuilder html, Precept precept, string version, Option<Guid> acted, Option<RefusedPrecept> refused)
    {
        var id = precept.Id.ToString("D");
        var action = $"/scriptorium/precepts/{id}";
        var named = $"""<span class="visually-hidden">: {Encode(precept.Text)}</span>""";
        var origin = precept.ByScribe ? "A Scribe's words" : "Distilled by the Machine Spirit";
        var pin = precept.Pinned
            ? $"""<button title="Lets the next distillation replace this precept with a new one." class="outline" formaction="{action}/unpin">{ScriptoriumIcons.Pin}Unpin{named}</button>"""
            : $"""<button title="Keeps this precept, word for word, when the Precepts are distilled again. Pinned precepts come first." class="outline" formaction="{action}/pin">{ScriptoriumIcons.Pin}Pin{named}</button>""";
        html.Append($"""
            <li class="rite{(acted.Exists(found => found == precept.Id) ? " acted" : string.Empty)}" id="precept-{id}">
            <p class="text">{WithCode(precept.Text)}</p>
            <p class="meta">{(precept.Pinned ? """<span class="placed">Pinned</span> · """ : string.Empty)}{Encode(origin)}</p>
            <details class="fold"{(refused.IsSome ? " open" : string.Empty)}>
            <summary>{ScriptoriumIcons.Note}Edit{named}</summary>
            <form method="post" action="{action}/edit" class="amend">
            {version}
            <label for="precept-{id}-text">Its words</label>
            <p class="hint" id="precept-{id}-hint">Saving pins it, so distilling again keeps your words.</p>
            <textarea id="precept-{id}-text" name="text" rows="2" maxlength="{Precept.MaxLength}" aria-describedby="precept-{id}-hint{(refused.IsSome ? " precepts-status" : string.Empty)}"{(refused.IsSome ? " aria-invalid=\"true\"" : string.Empty)} required>{Encode(refused.Map(found => found.Text).IfNone(precept.Text))}</textarea>
            <div class="buttons"><button class="primary">{ScriptoriumIcons.Save}Save{named}</button></div>
            </form>
            </details>
            <form method="post" class="buttons">
            {version}
            {pin}
            <button class="danger burn" formaction="{action}/delete">{ScriptoriumIcons.Delete}Delete{named}</button>
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
    private static void Agreement(StringBuilder html, IReadOnlyList<AgreementMonth> trend)
    {
        html.Append($"""
            <section aria-labelledby="agreement-heading">
            <h2 id="agreement-heading">Do the Scribes Agree with the Augury?</h2>
            <p>How many of the Augury's top {AgreementTrend.TopPickPlaces} picks the Scribes burned, and how far they moved
            drafts from the places it gave them. When both stay low, the Augury can be left to choose on its own.</p>
            <table class="agreement">
            <thead><tr><th scope="col">Month</th><th scope="col">Judgments</th>
            <th scope="col">Top picks burned</th><th scope="col">Moved, on average</th></tr></thead>
            <tbody>
            """);
        foreach (var month in trend)
        {
            var burned = month.DraftsBurned == 0 ? "No burns" : $"{month.AuguryTopPicksBurned} of {Count(month.DraftsBurned, "burn")}";
            var moved = month.AveragePlacesFromAuguryPlace.Match(
                Some: distance => $"{Places(distance)} ({Count(month.JudgedDraftsMoved, "move")})",
                None: () => "No moves");
            html.Append($"""<tr><th scope="row">{month.FirstDayUtc.ToString("MMM yyyy", CultureInfo.InvariantCulture)}</th>"""
                + $"<td>{month.ScribeDecisions}</td><td>{burned}</td><td>{moved}</td></tr>");
        }
        html.Append("</tbody></table></section>");
    }

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string Places(double distance)
        => distance == 1 ? "1 place" : $"{distance.ToString("0.#", CultureInfo.InvariantCulture)} places";

    private static string Distilled(PreceptBook book)
        => book.DistilledAtUtc.Match(
            Some: at => $"Distilled {at.ToString("ddd d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture)}"
                + $"{book.DistilledFrom.Map(count => $" from {count} judgments").IfNone(string.Empty)}, and again every Monday.",
            None: () => "Not distilled yet: they're distilled every Monday from the Scribes' judgments.");

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
        "text-needed" => PreceptKeeping.TextNeeded.Message,
        "too-long" => PreceptKeeping.TooLong.Message,
        "full" => PreceptKeeping.Full.Message,
        _ => "The Precepts changed meanwhile: another Scribe, or a distillation. Look again.",
    });
}
