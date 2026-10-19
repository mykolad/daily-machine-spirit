using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Expurgation;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

public static partial class ScriptoriumPage
{
    /// <summary>The Expurgator's desk: the newest published rites to choose from, and any other by its number.</summary>
    public static string RenderExpurgations(IReadOnlyList<Rite> newest, Option<string> message)
    {
        var html = new StringBuilder();
        Head(html, "The Expurgator's Desk");
        html.Append("""<main id="main">""");
        Heading(html, "The Expurgator's Desk",
            "Choose a published rite, then the words to lose to time. Each becomes an emoji, everywhere the site keeps it.");
        html.Append($"""
            <p><a href="/scriptorium">Back to the Scriptorium</a></p>
            <p class="status" role="status">{message.Map(Encode).IfNone(string.Empty)}</p>
            <form method="get" action="/scriptorium/expurgate" class="number-form">
            <label for="rite-number">A rite by its number</label>
            <input type="number" id="rite-number" name="number" min="1" inputmode="numeric" required>
            <button class="secondary">{ScriptoriumIcons.Note}Open It</button>
            </form>
            <section aria-labelledby="newest-heading"><h2 id="newest-heading">The newest rites</h2>
            """);
        if (newest.Count == 0)
        {
            html.Append("<p>No rite has been published yet.</p>");
        }
        else
        {
            html.Append("""<ul class="judgments">""");
            foreach (var rite in newest)
            {
                html.Append($"""<li><span class="when">NO. {rite.Number} · {Date(rite.PublishedOnUtc)}</span> <a href="/scriptorium/expurgate/{rite.Number}">{WithCode(rite.Writing.Title)}</a></li>""");
            }
            html.Append("</ul>");
        }
        html.Append("</section></main>");
        Footer(html, None);
        return html.ToString();
    }

    /// <summary>
    /// A published rite with each of its words a checkbox, to choose the ones to erase: plain HTML, the chosen ones struck
    /// through. The choice goes to the confirming page, which changes nothing.
    /// </summary>
    /// <param name="chosen">Words chosen before (<c>text:12</c>), checked again when the Expurgator chooses again. The form
    /// carries the words' version, so places chosen in words another erasure changed meanwhile are chosen again.</param>
    public static string RenderExpurgate(ExpurgatableRite expurgatable, IReadOnlyCollection<string> chosen, Option<string> message)
    {
        var rite = expurgatable.Rite;
        var html = new StringBuilder();
        Head(html, $"Expurgate NO. {rite.Number}");
        html.Append("""<main id="main">""");
        Heading(html, "Expurgate a Rite",
            "Choose the words to lose to time. Nothing changes until you've seen the rite without them, and said so.");
        html.Append($"""
            <p><a href="/scriptorium/expurgate">The Expurgator's Desk</a> · <a href="{Pages.Paths.Rite(rite.Number)}">The rite's page</a></p>
            <p class="status" role="status">{message.Map(Encode).IfNone(string.Empty)}</p>
            <p class="kicker">NO. {rite.Number} · {Date(rite.PublishedOnUtc)} · {Kind(rite.Writing.Kind)}</p>
            <form method="get" action="/scriptorium/expurgate/{rite.Number}/confirm">
            <input type="hidden" name="version" value="{Encode(expurgatable.Version)}">
            """);
        foreach (var part in RiteParts.All)
        {
            html.Append($"""<fieldset class="part"><legend>{part.Label()}</legend><p class="words">""");
            var word = 0;
            foreach (var piece in Wording.Split(part.Of(rite.Writing)))
            {
                if (!piece.IsWord)
                {
                    html.Append(Encode(piece.Text));
                    continue;
                }
                var value = RedactedWord.Chosen(part, word++);
                var check = chosen.Contains(value) ? " checked" : string.Empty;
                html.Append($"""<label class="word"><input type="checkbox" name="w" value="{value}"{check}>{Encode(piece.Text)}</label>""");
            }
            html.Append("</p></fieldset>");
        }
        html.Append($"""
            <div class="buttons"><button class="danger">{ScriptoriumIcons.Burn}See It Without Them</button></div>
            </form>
            </main>
            """);
        Footer(html, None);
        return html.ToString();
    }

    /// <summary>
    /// The rite as it will read, and what erasing means, before anything changes. The form carries the version of the
    /// words they were chosen from, and the emoji each becomes, so the rite reads afterwards as it does here.
    /// </summary>
    public static string RenderExpurgateConfirm(ExpurgatableRite expurgatable, IReadOnlyList<RedactedWord> chosen, RiteWriting redacted)
    {
        var rite = expurgatable.Rite;
        var parts = string.Join(", ", RiteParts.All
            .Select(part => (Part: part, Words: chosen.Count(word => word.Part == part)))
            .Where(counted => counted.Words > 0)
            .Select(counted => $"{counted.Words} {(counted.Words == 1 ? "word" : "words")} of its {counted.Part.Name()}"));
        var again = string.Concat(chosen.Select(word => $"&w={Uri.EscapeDataString(RedactedWord.Chosen(word.Part, word.Word))}"));
        var html = new StringBuilder();
        Head(html, $"Erase words from NO. {rite.Number}?");
        html.Append("""<main id="main">""");
        Heading(html, "Erase These Words?",
            "This is the rite as it will read. The words are erased everywhere the site keeps them, and nobody can bring them back.");
        html.Append($"""
            <p class="status" role="status">{Encode($"Erasing {parts}: from the rite, the draft it was published from, the Scribes' decisions on it and the Record. The Record will say you erased them, not what they were.")}</p>
            <article class="rite" aria-labelledby="title-preview">
            <p class="kicker">NO. {rite.Number} · {Date(rite.PublishedOnUtc)} · {Kind(rite.Writing.Kind)}</p>
            <h3 id="title-preview">{WithCode(redacted.Title)}</h3>
            <p class="text">{WithCode(redacted.Text)}</p>
            <p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(redacted.HereticalTruth)}</p>
            </article>
            <form method="post" action="/scriptorium/expurgate/{rite.Number}">
            <input type="hidden" name="version" value="{Encode(expurgatable.Version)}">
            {string.Concat(chosen.Select(word => $"""<input type="hidden" name="r" value="{Encode(word.ToString())}">"""))}
            <div class="buttons">
            <button class="danger">{ScriptoriumIcons.Burn}Erase Them for Good</button>
            <a class="button-link" href="{Encode($"/scriptorium/expurgate/{rite.Number}?version={expurgatable.Version}{again}")}">Choose Again</a>
            </div>
            </form>
            </main>
            """);
        Footer(html, None);
        return html.ToString();
    }
}
