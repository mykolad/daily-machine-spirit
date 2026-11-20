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
    // The readings' numerals, as the design sets them.
    private static readonly string[] Numerals = ["I.", "II.", "III."];

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
            <div class="revelation">
            {Guess(rite)}
            <section class="truth" id="truth-panel" aria-labelledby="truth-heading" tabindex="-1" hidden>
            <div class="truth-prompt" aria-hidden="true">$ explain --plainly</div>
            <div class="truth-body">
            <h2 id="truth-heading">Heretical Truth</h2>
            <p>{Html.WithInlineCode(rite.Writing.HereticalTruth)}</p>
            </div>
            </section>
            </div>
            <div class="actions">
            <div class="faithful">
            <p class="margin-label" aria-hidden="true">The faithful say</p>
            <div class="reactions" role="group" aria-label="Reactions" data-rite="{rite.Number}" data-counted-at="{countedAt.ToUnixTimeMilliseconds()}">
            <button type="button" class="button reaction reaction-blessed" data-reaction="blessed" aria-pressed="false">{Svg.FlameIcon}<span>Blessed</span><span class="count">{Html.Count(rite.BlessedCount)}</span></button>
            <button type="button" class="button reaction reaction-heresy" data-reaction="heresy" aria-pressed="false">{Svg.SkullIcon}<span>Heresy</span><span class="count">{Html.Count(rite.HeresyCount)}</span></button>
            </div>
            <div class="vote-status" data-signed-in="false" hidden><a href="{Html.Encode(Paths.SignIn(here))}">Sign in with GitHub</a> to vote<span class="vote-privacy">Only a one-way key made from your account's id is kept: never your name, email or GitHub token.</span></div>
            <div class="vote-status" data-signed-in="true" hidden>Voting with your GitHub account · <a href="{Paths.Breviary}">Your Breviary</a> · <form method="post" action="{Html.Encode(Paths.SignOutFrom(here))}"><button type="submit" class="text-button">Sign out</button></form></div>
            {Confession(rite)}
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

    /// <summary>
    /// Confess (#200), "I have done this": apart from the verdicts, under them. The script shows it, and presses it for a
    /// reader who confessed; without the script it stays hidden, as voting needs the script anyway. The count is read
    /// with the button (<c>aria-describedby</c>); the script says it again after a press, in the reader's words.
    /// </summary>
    private static string Confession(Rite rite)
        => $"""
            <div class="confession" hidden>
            <span class="confession-rule" aria-hidden="true"><svg width="8" height="8" viewBox="0 0 8 8" focusable="false"><path d="M4 0l4 4-4 4-4-4z" fill="#c9a25a"/></svg></span>
            <button type="button" class="confess" aria-pressed="false" aria-describedby="confessed-count" data-confess>{Svg.CandleIcon}I have done this</button>
            <p class="confessed-count" id="confessed-count" data-confessions="{rite.ConfessionCount}">{ConfessedWords(rite.ConfessionCount)}</p>
            <p class="confess-status" role="status"></p>
            </div>
            """;

    // As the script words it too, which also knows when the one is the reader ("Thou alone hast confessed").
    private static string ConfessedWords(int count) => count switch
    {
        0 => "None have confessed",
        1 => "1 hath confessed",
        _ => $"{Html.Count(count)} have confessed",
    };

    /// <summary>
    /// Guessing the Heretical Truth (#199): the first press of the seal cracks it, and the truth and the two false ones
    /// unfold under it as three readings, in an order fixed per rite; choosing one breaks the seal. The script checks the
    /// answer in the browser (the truth is in the page already), so it all stays hidden until it shows it, and the
    /// parchment never changes. A rite without false truths has none of it. After the break the verdict, and a wrong
    /// guess struck through, sit above the truth.
    /// </summary>
    private static string Guess(Rite rite)
        => rite.Writing.FalseTruths.Match(
            None: () => string.Empty,
            Some: falseTruths =>
            {
                var truthAt = TruthPlace(rite.Number);
                var others = new Queue<string>([falseTruths.First, falseTruths.Second]);
                var readings = Enumerable.Range(1, 3)
                    .Select(place => Reading(place, place == truthAt ? rite.Writing.HereticalTruth : others.Dequeue()));
                return $"""
                    <div class="guess" data-guess data-truth="{truthAt}">
                    <div class="readings" id="guess-readings" role="group" aria-labelledby="guess-prompt" data-readings hidden>
                    <p class="guess-prompt" id="guess-prompt">What is the Heretical Truth?</p>
                    {string.Concat(readings)}
                    <button type="button" class="guess-skip" data-guess-skip>Just show me</button>
                    </div>
                    <div class="guess-verdict" role="status" data-verdict></div>
                    <div class="guessed" data-guessed hidden></div>
                    </div>
                    """;
            });

    /// <summary>Where the truth stands among the three readings: the same for a rite every time, but not always first.</summary>
    private static int TruthPlace(int number) => (int)(((uint)number * 2654435761u) >> 16) % 3 + 1;

    // A button, since a choice confirms at once; its name begins with its numeral ("II. An empty catch…").
    private static string Reading(int place, string words)
        => $"""
            <button type="button" class="reading" data-reading="{place}"><span class="reading-num">{Numerals[place - 1]}{Svg.ReadingRing}</span><span class="reading-text">{Html.WithInlineCode(words)}</span></button>
            """;

    // The first letter becomes the drop cap, hidden from screen readers, which read a hidden copy so the first word
    // stays whole. A text that starts with code or punctuation gets no drop cap.
    private static string Prayer(string text)
        => text.Length > 1 && char.IsLetter(text[0])
            ? $"""<span class="drop-cap" aria-hidden="true">{Html.Encode(text[..1])}</span><span class="sr-only">{Html.Encode(text[..1])}</span>{Html.WithInlineCode(text[1..])}"""
            : Html.WithInlineCode(text);
}
