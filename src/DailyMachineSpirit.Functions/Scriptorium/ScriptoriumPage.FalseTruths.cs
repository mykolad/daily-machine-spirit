using System.Text;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

public static partial class ScriptoriumPage
{
    /// <summary>
    /// False truths for what was written before them (#199): how many rites and drafts still lack them, a button to have
    /// them written, and each published rite's proposal, beside the words a reader will see it with, to accept as it
    /// is, amend, or have written again.
    /// </summary>
    /// <param name="without">How many rites and drafts have neither false truths nor a proposal.</param>
    /// <param name="message">What the last action did, or why it was refused.</param>
    public static string RenderFalseTruths(int without, IReadOnlyList<ProposedFalseTruths> proposals, Option<string> message)
    {
        var html = new StringBuilder();
        Head(html, "False Truths");
        html.Append("""<main id="main">""");
        Heading(html, "False Truths",
            "Readers guess the Heretical Truth among it and two false ones. Rites written before that need them: drafts get them as they're written, published rites wait for your word here.");
        html.Append($"""
            <p><a href="/scriptorium">Back to the Scriptorium</a></p>
            <p class="status" role="status">{message.Map(Encode).IfNone(string.Empty)}</p>
            """);
        html.Append(without == 0
            ? "<p>Every rite and draft has false truths, or a proposal below.</p>"
            : $"""
                <form method="post" action="/scriptorium/false-truths/write">
                <p>{without} {(without == 1 ? "rite or draft has" : "rites and drafts have")} none yet. The Machine Spirit writes them one at a time: reload this page to see them arrive.</p>
                <button class="primary" data-slow="The Machine Spirit is asked…">{ScriptoriumIcons.Summon}Write the False Truths</button>
                </form>
                """);
        html.Append("""<section aria-labelledby="proposed-heading"><h2 id="proposed-heading">To accept</h2>""");
        if (proposals.Count == 0)
        {
            html.Append("<p>No proposal waits.</p>");
        }
        foreach (var proposal in proposals)
        {
            var id = $"proposal-{proposal.Number}";
            html.Append($"""
                <article class="rite" id="{id}" aria-labelledby="{id}-title">
                <p class="kicker">NO. {proposal.Number} · {Kind(proposal.Kind)}</p>
                <h3 id="{id}-title">{WithCode(proposal.Title)}</h3>
                <p class="text">{WithCode(proposal.Text)}</p>
                <p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(proposal.HereticalTruth)}</p>
                <form method="post" class="amend" action="/scriptorium/false-truths/{proposal.Number}/accept">
                <label for="{id}-first">First False Truth</label>
                <textarea id="{id}-first" name="false-truth-1" rows="2" maxlength="{RiteWriting.MaxHereticalTruthLength}" required>{Encode(proposal.Proposed.First)}</textarea>
                <label for="{id}-second">Second False Truth</label>
                <textarea id="{id}-second" name="false-truth-2" rows="2" maxlength="{RiteWriting.MaxHereticalTruthLength}" required>{Encode(proposal.Proposed.Second)}</textarea>
                <div class="buttons">
                <button class="primary" aria-describedby="{id}-title">{ScriptoriumIcons.Save}Accept</button>
                <button class="secondary" formaction="/scriptorium/false-truths/{proposal.Number}/again" formnovalidate aria-describedby="{id}-title">{ScriptoriumIcons.Summon}Write Them Again</button>
                </div>
                </form>
                </article>
                """);
        }
        html.Append("</section></main>");
        Footer(html, Some(SlowActionScript.Js));
        return html.ToString();
    }
}
