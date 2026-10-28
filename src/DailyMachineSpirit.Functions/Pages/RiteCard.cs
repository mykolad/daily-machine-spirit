using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A rite as Today and its own page show it: the parchment, the wax seal that hides the Heretical Truth, and the
/// actions under it. Every focusable control sits on the dark background, where the amber focus ring shows.
/// </summary>
/// <remarks>
/// On a wide screen (<see cref="Styles"/>) the page gets margins: the votes move to the right one, and the left one
/// holds the margin notes and the tags. Nothing there repeats what the parchment says (its number, its date). The markup
/// stays in reading order, page first, and the stylesheet places it: a reader hears the rite before its margins.
/// </remarks>
public static class RiteCard
{
    // The purity seal's paper strip, chosen by the rite's number so a rite always wears the same one.
    private static readonly string[] PurityStrips = ["Tests passed (once)", "LGTM", "Approved by the Machine Spirit", "--no-verify"];

    /// <param name="ownPage">On its own page the title is the page's h1 and the text can be copied; on Today the
    /// masthead is the h1.</param>
    /// <param name="countedAt">When the rite's counts were read, no later: the script trusts a vote made since over the
    /// page, which a cache may have kept from before it.</param>
    /// <param name="marginNotes">What the left margin shows above the tags on a wide screen (Today's countdown), or
    /// empty.</param>
    /// <param name="places">The places of the rites the Reliquary keeps: the rite's, if kept, is in the left margin.</param>
    public static string Render(Rite rite, bool ownPage, DateTimeOffset countedAt, string marginNotes, IReadOnlyDictionary<int, int> places)
    {
        var heading = ownPage ? "h1" : "h2";
        var kind = rite.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL";
        var url = Paths.Rite(rite.Number);
        // Where signing in or out comes back to: this page.
        var here = ownPage ? url : Paths.Today;
        var copyText = $"{rite.Writing.Title}\n\n{rite.Writing.Text}\n\nHeretical Truth: {rite.Writing.HereticalTruth}";
        var copyButton = ownPage
            ? $"""<button type="button" class="button button-ghost" data-copy="text" data-url="{url}" data-text="{Html.Encode(copyText)}">{Svg.CopyIcon}Copy text</button>"""
            : string.Empty;
        var tags = RiteTags.Of(rite.Tags);
        var filedUnder = tags.Count == 0 ? string.Empty : $"""<p class="tags"><span class="margin-label">Filed under</span> {TagLinks.Render(tags)}</p>""";
        // The badge says it's in the Reliquary, so its label is for the eye alone.
        var badge = RelicBadge.For(rite.Number, places);
        var relic = badge.Length == 0 ? string.Empty : $"""<div class="relic-note"><p class="margin-label" aria-hidden="true">In the Reliquary</p>{badge}</div>""";
        var margin = marginNotes.Length == 0 && relic.Length == 0 && filedUnder.Length == 0
            ? string.Empty
            : $"""
                <aside class="marginalia" aria-label="About this rite">
                {marginNotes}
                {relic}
                {filedUnder}
                </aside>
                """;
        return $"""
            <div class="hero">
            <div class="rite">
            <article class="parchment" aria-labelledby="rite-title">
            <span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>
            {Svg.Corners}
            <span class="margin-text margin-left" aria-hidden="true">52 45 41 44 20 54 48 45 20 43 4F 44 45 · 52 45 41 44 20 54 48 45 20 43 4F 44 45</span>
            <span class="margin-text margin-right" aria-hidden="true">01001011 01001110 01001111 01010111 · 01001011 01001110</span>
            <div class="parchment-body">
            <p class="kicker">A {kind} · NO. {rite.Number}</p>
            <{heading} class="rite-title" id="rite-title">{Html.Encode(rite.Writing.Title)}</{heading}>
            {ImperialDate.WithTheDay(rite.PublishedOnUtc, "rite-day", "rite-date")}
            {Svg.RubricDivider}
            <p class="prayer">{Prayer(rite.Writing.Text)}</p>
            <div class="colophon">
            <span class="inscribed">Inscribed by {Html.Encode(rite.Writing.GeneratedByModel)}</span>
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
            <p>{Html.WithInlineCode(rite.Writing.HereticalTruth)}</p>
            </div>
            </section>
            <div class="actions">
            <div class="faithful">
            <p class="margin-label" aria-hidden="true">The faithful say</p>
            <div class="reactions" role="group" aria-label="Reactions" data-rite="{rite.Number}" data-counted-at="{countedAt.ToUnixTimeMilliseconds()}">
            <button type="button" class="button reaction reaction-blessed" data-reaction="blessed" aria-pressed="false">{Svg.FlameIcon}<span>Blessed</span><span class="count">{Html.Count(rite.BlessedCount)}</span></button>
            <button type="button" class="button reaction reaction-heresy" data-reaction="heresy" aria-pressed="false">{Svg.SkullIcon}<span>Heresy</span><span class="count">{Html.Count(rite.HeresyCount)}</span></button>
            </div>
            <div class="vote-status" data-signed-in="false" hidden><a href="{Html.Encode(Paths.SignIn(here))}">Sign in with GitHub</a> to vote<span class="vote-privacy">Only a one-way key made from your account's id is kept: never your name, email or GitHub token.</span></div>
            <div class="vote-status" data-signed-in="true" hidden>Voting with your GitHub account · <a href="{Paths.Breviary}">Your Breviary</a> · <form method="post" action="{Html.Encode(Paths.SignOutFrom(here))}"><button type="submit" class="text-button">Sign out</button></form></div>
            </div>
            <div class="sharing">
            <button type="button" class="button button-ghost" data-copy="link" data-url="{url}">{Svg.LinkIcon}Share link</button>
            {copyButton}
            </div>
            </div>
            {margin}
            {Svg.AltarCandles}
            {Svg.Traces}
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
