using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

public static partial class ScriptoriumPage
{
    /// <summary>
    /// Where a Scribe chooses the name the Record shows them by (#84). The field holds a name to take: the one they
    /// have, else the first suggestion, which they can change. Each suggestion is a link that puts it in the field
    /// instead; Suggest Others asks for new ones. Plain links and a form, without a script.
    /// </summary>
    /// <param name="yours">How the Record shows them now; none on staging, where there's no name to choose.</param>
    /// <param name="inTheField">What the field holds.</param>
    /// <param name="message">Why a name wasn't taken, or why there's none to choose.</param>
    public static string RenderName(Option<NameInTheRecord> yours, IReadOnlyList<string> suggestions, string inTheField, Option<string> message)
    {
        var html = new StringBuilder();
        Head(html, "Your Name in the Record");
        html.Append("""<main id="main">""");
        Heading(html, "Your Name in the Record",
            "The Record shows who made each change. Choose the name it shows for you: a name or an alias, which only the Scribes see.");
        html.Append("""<p class="record-link"><a href="/scriptorium">Back to the Scriptorium</a> · <a href="/scriptorium/record">The Record</a></p>""");
        html.Append($"""<p class="status" role="status" id="name-status">{message.Map(Encode).IfNone(string.Empty)}</p>""");
        yours.IfSome(shown => NameForm(html, shown, suggestions, inTheField, message.IsSome));
        html.Append("</main>");
        Footer(html, withScript: false);
        return html.ToString();
    }

    private static void NameForm(StringBuilder html, NameInTheRecord yours, IReadOnlyList<string> suggestions, string inTheField, bool refused)
    {
        // Each suggestion's link carries all of them, so following it keeps the same ones rather than asking anew.
        var carried = string.Concat(suggestions.Select(name => $"&s={Uri.EscapeDataString(name)}"));
        var links = string.Concat(suggestions.Select(name =>
            $"""<li><a class="button-link" href="{Encode($"/scriptorium/name?name={Uri.EscapeDataString(name)}{carried}#scribe-name")}">{Encode(name)}</a></li>"""));
        var described = refused ? "scribe-name-hint name-status" : "scribe-name-hint";
        html.Append($"""
            <p>The Record {(yours.Chosen ? "calls you" : "shows you as")} “{Encode(yours.Shown)}”.</p>
            <section aria-labelledby="suggestions-heading">
            <h2 id="suggestions-heading">Names the Machine Spirit Suggests</h2>
            <ul class="suggestions">{links}<li><a class="button-link" href="/scriptorium/name">Suggest Others</a></li></ul>
            </section>
            <form method="post" action="/scriptorium/name" class="amend">
            {string.Concat(suggestions.Select(name => $"""<input type="hidden" name="s" value="{Encode(name)}">"""))}
            <label for="scribe-name">Your name</label>
            <p class="hint" id="scribe-name-hint">A suggestion goes here when you pick it, to take as it is or change. At most {ScribeName.MaxLength} characters, and not a name another Scribe has.</p>
            <input type="text" id="scribe-name" name="name" value="{Encode(inTheField)}" maxlength="{ScribeName.MaxLength}" aria-describedby="{described}"{(refused ? " aria-invalid=\"true\"" : string.Empty)} autofocus required>
            <div class="buttons">
            <button class="primary">{ScriptoriumIcons.Save}Take This Name</button>
            <a class="button-link" href="/scriptorium">Leave It as It Is</a>
            </div>
            </form>
            """);
    }
}
