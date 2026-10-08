using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A rite as Today and its own page show it: the parchment, the wax seal that hides the Heretical Truth, and the
/// actions under it. Every focusable control sits on the dark background, where the amber focus ring shows.
/// </summary>
public static class RiteCard
{
    // The purity seal's paper strip, chosen by the rite's number so a rite always wears the same one.
    private static readonly string[] PurityStrips = ["Tests passed (once)", "LGTM", "Approved by the Machine Spirit", "--no-verify"];

    /// <param name="ownPage">On its own page the title is the page's h1 and the text can be copied; on Today the
    /// masthead is the h1.</param>
    public static string Render(Rite rite, bool ownPage)
    {
        var heading = ownPage ? "h1" : "h2";
        var kind = rite.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL";
        var url = Paths.Rite(rite.Number);
        var copyText = $"{rite.Title}\n\n{rite.Text}\n\nHeretical Truth: {rite.HereticalTruth}";
        var copyButton = ownPage
            ? $"""<button type="button" class="button button-ghost" data-copy="text" data-url="{url}" data-text="{Html.Encode(copyText)}">{Svg.CopyIcon}Copy text</button>"""
            : string.Empty;
        return $"""
            <div class="rite">
            <article class="parchment" aria-labelledby="rite-title">
            <span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>
            {Svg.Corners}
            <span class="margin-text margin-left" aria-hidden="true">52 45 41 44 20 54 48 45 20 43 4F 44 45 · 52 45 41 44 20 54 48 45 20 43 4F 44 45</span>
            <span class="margin-text margin-right" aria-hidden="true">01001011 01001110 01001111 01010111 · 01001011 01001110</span>
            <div class="parchment-body">
            <p class="kicker">A {kind} · NO. {rite.Number}</p>
            <{heading} class="rite-title" id="rite-title">{Html.Encode(rite.Title)}</{heading}>
            <time class="rite-date" datetime="{Html.IsoDate(rite.PublishedOnUtc)}">{Html.LongDate(rite.PublishedOnUtc)}</time>
            {Svg.RubricDivider}
            <p class="prayer">{Prayer(rite.Text)}</p>
            <div class="colophon">
            <span class="inscribed">Inscribed by {Html.Encode(rite.GeneratedByModel)}</span>
            <span class="purity" aria-hidden="true"><span class="purity-strip">{PurityStrips[rite.Number % PurityStrips.Length]}</span>{Svg.PuritySeal}</span>
            </div>
            </div>
            </article>
            <div class="ribbon" aria-hidden="true"><span></span><span></span></div>
            <button type="button" class="seal" aria-expanded="false" aria-controls="truth-panel">
            {Svg.BreakableSeal}
            <span class="seal-label">Break the seal</span>
            <span class="seal-sub">Reveal the Heretical Truth</span>
            </button>
            </div>
            <section class="truth" id="truth-panel" aria-labelledby="truth-heading" hidden>
            <div class="truth-prompt" aria-hidden="true">$ explain --plainly</div>
            <div class="truth-body">
            <h2 id="truth-heading">Heretical Truth</h2>
            <p>{Html.WithInlineCode(rite.HereticalTruth)}</p>
            </div>
            </section>
            <div class="actions">
            <div class="reactions" role="group" aria-label="Reactions" data-rite="{rite.Number}">
            <button type="button" class="button reaction reaction-blessed" data-reaction="blessed" aria-pressed="false">{Svg.FlameIcon}<span>Blessed</span><span class="count">{Html.Count(rite.BlessedCount)}</span></button>
            <button type="button" class="button reaction reaction-heresy" data-reaction="heresy" aria-pressed="false">{Svg.SkullIcon}<span>Heresy</span><span class="count">{Html.Count(rite.HeresyCount)}</span></button>
            </div>
            <button type="button" class="button button-ghost" data-copy="link" data-url="{url}">{Svg.LinkIcon}Share link</button>
            {copyButton}
            </div>
            """;
    }

    // The first letter becomes the drop cap, hidden from screen readers, which read a hidden copy so the first word
    // stays whole. A text that starts with code or punctuation gets no drop cap.
    private static string Prayer(string text)
        => text.Length > 1 && char.IsLetter(text[0])
            ? $"""<span class="drop-cap" aria-hidden="true">{Html.Encode(text[..1])}</span><span class="sr-only">{Html.Encode(text[..1])}</span>{Html.WithInlineCode(text[1..])}"""
            : Html.WithInlineCode(text);
}
