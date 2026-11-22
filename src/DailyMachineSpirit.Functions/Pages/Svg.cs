namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The design's original artwork (design handoff), inline: decorative, so hidden from assistive technology.</summary>
public static class Svg
{
    private const string CogPaths =
        """<path fill="#c9a25a" fill-rule="evenodd" d="M27.71 10.42L28.14 3.26A29 29 0 0 1 35.86 3.26L36.29 10.42A22 22 0 0 1 44.22 13.71L49.59 8.95A29 29 0 0 1 55.05 14.41L50.29 19.78A22 22 0 0 1 53.58 27.71L60.74 28.14A29 29 0 0 1 60.74 35.86L53.58 36.29A22 22 0 0 1 50.29 44.22L55.05 49.59A29 29 0 0 1 49.59 55.05L44.22 50.29A22 22 0 0 1 36.29 53.58L35.86 60.74A29 29 0 0 1 28.14 60.74L27.71 53.58A22 22 0 0 1 19.78 50.29L14.41 55.05A29 29 0 0 1 8.95 49.59L13.71 44.22A22 22 0 0 1 10.42 36.29L3.26 35.86A29 29 0 0 1 3.26 28.14L10.42 27.71A22 22 0 0 1 13.71 19.78L8.95 14.41A29 29 0 0 1 14.41 8.95L19.78 13.71A22 22 0 0 1 27.71 10.42ZM46 32A14 14 0 1 0 18 32A14 14 0 1 0 46 32Z"></path><path d="M24.5 26.5l5.5 5.5-5.5 5.5M32.5 38.5h7" fill="none" stroke="#c9a25a" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round"></path>""";

    private const string WaxSeal =
        """<svg viewBox="0 0 64 64" width="92" height="92"><circle cx="32" cy="32" r="27" fill="#8f2a20"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="#8f2a20" stroke-width="5" stroke-dasharray="4 5" stroke-linecap="round"></circle><circle cx="32" cy="32" r="19" fill="none" stroke="#6b1c14" stroke-width="2.5"></circle><text x="32" y="38.5" text-anchor="middle" style="font-family:var(--mono)" font-size="17" font-weight="500" fill="#5c140e">{ }</text><ellipse cx="22" cy="19" rx="7" ry="3" fill="#b8473a" opacity=".6" transform="rotate(-35 22 19)"></ellipse></svg>""";

    private const string Corner =
        """<path d="M2 58V14L14 2h44" stroke="#a88a55" stroke-width="1.5"></path><path d="M10 40V18l8-8h22" stroke="#a88a55" stroke-width="1.5"></path><circle cx="40" cy="10" r="2.4" fill="#a88a55"></circle><circle cx="10" cy="40" r="2.4" fill="#a88a55"></circle>""";

    private const string Flame =
        """<ellipse cx="10" cy="9" rx="4" ry="7.5" fill="#f0b45a"></ellipse><rect x="4" y="18" width="12" height="26" rx="3" fill="#e9dcbc"></rect>""";

    public const string BrandCog = $"""<svg aria-hidden="true" focusable="false" viewBox="0 0 64 64" width="28" height="28">{CogPaths}</svg>""";

    // The tab's icon: the cog on the site's background, a little inset so the rounded corners don't clip its teeth. Inline
    // as a data URI, so it costs no request; and with an icon declared, browsers stop asking for /favicon.ico, which
    // would get the 404 page.
    public static readonly string Favicon = "data:image/svg+xml," + Uri.EscapeDataString(
        $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="12" fill="#100e0b"/><g transform="translate(4 4) scale(.875)">{CogPaths}</g></svg>""");

    public const string MastheadRule =
        $"""<svg class="masthead-rule" viewBox="0 0 320 40" width="320" height="40" fill="none" aria-hidden="true"><path d="M4 20h104l8-8h18M316 20H212l-8-8h-18" stroke="#84662c" stroke-width="1.5"></path><path d="M60 20l8 8h40M260 20l-8 8h-40" stroke="#84662c" stroke-width="1.5"></path><circle cx="4" cy="20" r="2.5" fill="#84662c"></circle><circle cx="316" cy="20" r="2.5" fill="#84662c"></circle><svg x="140" y="0" width="40" height="40" viewBox="0 0 64 64">{CogPaths}</svg></svg>""";

    public const string Corners =
        $"""<svg class="corner corner-tl" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-tr" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-bl" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-br" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg>""";

    // The ring a reader inks around the reading they chose (#199), as one circles a line on a page: shown by the
    // stylesheet on the checked reading alone. Its stroke keeps its width however far the ring stretches.
    public const string ReadingRing =
        """<svg class="reading-ring" aria-hidden="true" focusable="false" viewBox="0 0 46 34" preserveAspectRatio="none" fill="none"><path vector-effect="non-scaling-stroke" d="M24 3.5C12.5 2.6 3.4 8.6 3.2 16.9 3 25.6 12.6 31 24.2 30.6 35.4 30.2 43.4 24.3 42.8 16.2 42.2 8.1 33 3.4 20.5 4.6" stroke="#8f2a20" stroke-width="1.8" stroke-linecap="round"></path></svg>""";

    public const string RubricDivider =
        """<svg class="divider" viewBox="0 0 120 12" width="120" height="12" fill="none" aria-hidden="true"><path d="M0 6h46M74 6h46" stroke="#8f2a20" stroke-width="1.2"></path><path d="M60 0l6 6-6 6-6-6z" fill="#8f2a20"></path></svg>""";

    /// <summary>The Archive's seal at its end: Today's wax seal, whole and never broken.</summary>
    public const string ClosingSeal = $"""<span class="closing-seal">{WaxSeal}</span>""";

    public const string PuritySeal =
        """<svg viewBox="0 0 64 64" width="40" height="40"><circle cx="32" cy="32" r="27" fill="#8f2a20"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="#8f2a20" stroke-width="5" stroke-dasharray="4 5" stroke-linecap="round"></circle><circle cx="32" cy="32" r="19" fill="none" stroke="#6b1c14" stroke-width="2.5"></circle><path d="M27 26l6 6-6 6M35 39h6" stroke="#5c140e" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round" fill="none"></path></svg>""";

    /// <summary>The seal twice, each copy clipped to one side of the crack (<see cref="Styles"/> moves the halves apart).</summary>
    public const string BreakableSeal =
        $"""<span class="seal-wax" aria-hidden="true"><span class="seal-half seal-left">{WaxSeal}</span><span class="seal-half seal-right">{WaxSeal}</span></span>""";

    public const string CandleLeft = $"""<svg class="candle" viewBox="0 0 20 44" width="20" height="44" fill="none" aria-hidden="true">{Flame}</svg>""";

    public const string CandleRight = $"""<svg class="candle candle-late" viewBox="0 0 20 44" width="20" height="44" fill="none" aria-hidden="true">{Flame}</svg>""";

    // The wide screen's candles, one beside the rite in each margin: outlined, so they stay behind the page rather than
    // compete with it. Only the flame moves (Styles).
    private const string AltarCandlePaths =
        """<path class="altar-flame" d="M12 4C15 9 15 13 12 16C9 13 9 9 12 4Z" fill="#f0b45a" stroke="#f0b45a"></path><path d="M12 16V20"></path><rect x="7" y="20" width="10" height="34" rx="1.5"></rect><path d="M3 56H21"></path>""";

    public const string AltarCandleLeft =
        $"""<span class="altar altar-left" aria-hidden="true"><svg aria-hidden="true" focusable="false" viewBox="0 0 24 60" width="48" height="120" fill="none" stroke="#84662c" stroke-width="1.4" stroke-linecap="round">{AltarCandlePaths}</svg></span>""";

    public const string AltarCandleRight =
        $"""<span class="altar altar-right" aria-hidden="true"><svg aria-hidden="true" focusable="false" viewBox="0 0 24 60" width="48" height="120" fill="none" stroke="#84662c" stroke-width="1.4" stroke-linecap="round">{AltarCandlePaths}</svg></span>""";

    public const string AltarCandles = AltarCandleLeft + AltarCandleRight;

    // Circuit traces running in from the screen's edges on wide screens, drawn at half size (strokes 1.3px, dots 5px once
    // doubled). The right one is the same kind of drawing, mirrored by the stylesheet.
    public const string Traces =
        """<span class="traces traces-left" aria-hidden="true"><svg aria-hidden="true" focusable="false" viewBox="0 0 200 475" width="400" height="950" fill="none" stroke="#2f2820" stroke-width=".65"><path d="M0 90H60L80 110V260L100 280H140"></path><path d="M0 150H30L50 170V360L70 380H120"></path><path d="M0 330H40L60 310V220"></path><path d="M0 430H90L105 415H160"></path><circle cx="140" cy="280" r="2.5" fill="#2f2820"></circle><circle cx="120" cy="380" r="2.5" fill="#2f2820"></circle><circle cx="60" cy="220" r="2.5" fill="#2f2820"></circle><circle cx="160" cy="415" r="2.5" fill="#2f2820"></circle></svg></span><span class="traces traces-right" aria-hidden="true"><svg aria-hidden="true" focusable="false" viewBox="0 0 200 475" width="400" height="950" fill="none" stroke="#2f2820" stroke-width=".65"><path d="M0 120H50L70 140V300L90 320H150"></path><path d="M0 200H25L45 220V400L60 415H110"></path><path d="M0 60H80L95 75H130"></path><circle cx="150" cy="320" r="2.5" fill="#2f2820"></circle><circle cx="110" cy="415" r="2.5" fill="#2f2820"></circle><circle cx="130" cy="75" r="2.5" fill="#2f2820"></circle></svg></span>""";

    // The wax seal without its glyph, small enough for a recent rite's card.
    public const string SmallSeal =
        """<svg viewBox="0 0 64 64" width="32" height="32" aria-hidden="true"><circle cx="32" cy="32" r="27" fill="#8f2a20"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="#8f2a20" stroke-width="5" stroke-dasharray="4 5" stroke-linecap="round"></circle><circle cx="32" cy="32" r="19" fill="none" stroke="#6b1c14" stroke-width="2.5"></circle></svg>""";

    public const string SleepingSkull =
        """<svg viewBox="0 0 96 96" width="96" height="96" fill="none" aria-hidden="true"><path d="M20 46a28 28 0 0 1 56 0" stroke="#84662c" stroke-width="4"></path><path d="M48 22c-14 0-24 10-24 23 0 8 4 13 9 16v9a3 3 0 0 0 3 3h24a3 3 0 0 0 3-3v-9c5-3 9-8 9-16 0-13-10-23-24-23z" fill="#b6a98f"></path><path d="M34 48h10M52 48h10" stroke="#100e0b" stroke-width="3.5" stroke-linecap="round"></path><path d="M48 54l-3 5h6z" fill="#100e0b"></path><path d="M42 66v6M48 66v6M54 66v6" stroke="#100e0b" stroke-width="2"></path><rect x="14" y="42" width="10" height="18" rx="5" fill="#84662c"></rect><rect x="72" y="42" width="10" height="18" rx="5" fill="#84662c"></rect><text x="74" y="22" style="font-family:var(--mono)" font-size="12" fill="#84662c">z z</text></svg>""";

    public const string AwakeSkull =
        """<svg viewBox="0 0 96 96" width="96" height="96" fill="none" aria-hidden="true"><path d="M20 46a28 28 0 0 1 56 0" stroke="#c9a25a" stroke-width="4"></path><path d="M48 22c-14 0-24 10-24 23 0 8 4 13 9 16v9a3 3 0 0 0 3 3h24a3 3 0 0 0 3-3v-9c5-3 9-8 9-16 0-13-10-23-24-23z" fill="#e9dcbc"></path><circle cx="39" cy="47" r="5.5" fill="#100e0b"></circle><circle cx="57" cy="47" r="5.5" fill="#100e0b"></circle><path d="M48 54l-3 5h6z" fill="#100e0b"></path><path d="M42 66v6M48 66v6M54 66v6" stroke="#100e0b" stroke-width="2"></path><rect x="14" y="42" width="10" height="18" rx="5" fill="#c9a25a"></rect><rect x="72" y="42" width="10" height="18" rx="5" fill="#c9a25a"></rect><path d="M19 60c0 10 8 16 20 16" stroke="#c9a25a" stroke-width="3" stroke-linecap="round"></path><circle cx="41" cy="76" r="3" fill="#c9a25a"></circle></svg>""";

    // Blessed and Heresy: outlined, and filled by the stylesheet when pressed, so the state isn't colour alone. Lucide's
    // icons (Lucide-LICENSE.txt).
    public const string FlameIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"></path></svg>""";

    public const string SkullIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 22a1 1 0 0 0 1-1v-1a2 2 0 0 0 1.56-3.25 8 8 0 1 0-11.12 0A2 2 0 0 0 8 20v1a1 1 0 0 0 1 1z"></path><circle cx="9" cy="12" r="1"></circle><circle cx="15" cy="12" r="1"></circle><path d="m12.5 17-.5-1-.5 1h1z"></path></svg>""";

    // Confess (#200): a candle, lit when pressed, so the pressed state is a change of shape and not colour alone. Our own,
    // drawn in the verdict icons' weight.
    public const string CandleIcon =
        """<svg class="candle" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path class="flame" d="M12 2.5c1.7 1.9 2.2 3.5 0 5.4-2.2-1.9-1.7-3.5 0-5.4z" fill="#f0b45a" stroke="#f0b45a"/><path d="M12 8v3"/><rect x="8.5" y="11" width="7" height="10" rx="1.2"/><path d="M6 21h12"/></svg>""";

    public const string SmallFlameIcon =
        """<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"></path></svg>""";

    public const string SmallSkullIcon =
        """<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 22a1 1 0 0 0 1-1v-1a2 2 0 0 0 1.56-3.25 8 8 0 1 0-11.12 0A2 2 0 0 0 8 20v1a1 1 0 0 0 1 1z"></path><circle cx="9" cy="12" r="1"></circle><circle cx="15" cy="12" r="1"></circle></svg>""";

    // The most confessed (#200): a candle, our own, in the small icons' weight. Lit on the relic badge, its flame in the
    // badge's own colour; unlit beside a row's count of confessions, as the Confess button is before it's pressed.
    public const string SmallCandleIcon =
        """<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M12 2.5c1.7 1.9 2.2 3.5 0 5.4-2.2-1.9-1.7-3.5 0-5.4z" fill="currentColor"/><path d="M12 8v3"/><rect x="8.5" y="11" width="7" height="10" rx="1.2"/><path d="M6 21h12"/></svg>""";

    public const string SmallUnlitCandleIcon =
        """<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M12 8v3"/><rect x="8.5" y="11" width="7" height="10" rx="1.2"/><path d="M6 21h12"/></svg>""";

    public const string LinkIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path></svg>""";

    public const string CopyIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="13" height="13" x="9" y="9" rx="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>""";

    // Lucide's book-marked, for the nav's link to the reader's Breviary.
    public const string BreviaryIcon =
        """<svg aria-hidden="true" focusable="false" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M10 2v8l3-3 3 3V2"></path><path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H19a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H6.5a1 1 0 0 1 0-5H20"></path></svg>""";

    // Lucide's dices, for drawing a rite (#196), until it has an icon of its own.
    public const string DrawIcon =
        """<svg aria-hidden="true" focusable="false" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><rect width="12" height="12" x="2" y="10" rx="2" ry="2"></rect><path d="m17.92 14 3.5-3.5a2.24 2.24 0 0 0 0-3l-5-4.92a2.24 2.24 0 0 0-3 0L10 6"></path><path d="M6 18h.01"></path><path d="M10 14h.01"></path><path d="M15 6h.01"></path><path d="M18 9h.01"></path></svg>""";

    // Lucide's search and x, for the search fields: their colour comes from the stylesheet.
    public const string SearchIcon =
        """<svg aria-hidden="true" focusable="false" class="search-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="m21 21-4.34-4.34"></path><circle cx="11" cy="11" r="8"></circle></svg>""";

    public const string ClearIcon =
        """<svg aria-hidden="true" focusable="false" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"></path><path d="m6 6 12 12"></path></svg>""";

    public const string BackIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m12 19-7-7 7-7"></path><path d="M19 12H5"></path></svg>""";

    // Lucide's rss.
    public const string FeedIcon =
        """<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 11a9 9 0 0 1 9 9"></path><path d="M4 4a16 16 0 0 1 16 16"></path><circle cx="5" cy="19" r="1"></circle></svg>""";

    public const string RefreshIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"></path><path d="M21 3v5h-5"></path><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"></path><path d="M8 16H3v5"></path></svg>""";

    // The witnessed checkbox's tick (#198), drawn as a quill would: in currentColor, so the box sets it.
    public const string QuillTick =
        """<svg viewBox="0 0 24 24" width="12" height="12" focusable="false"><path d="M3 12.6C5.4 13 7.4 14.8 9.2 18.2C12 12.4 15.6 7.8 21.4 3.6C16.4 7.4 12.8 11.6 9.6 15.4C8.2 13.8 5.9 12.7 3 12.6Z" fill="currentColor"></path></svg>""";

    // The readers' ranks (#201, Ranks): one family, a shrine's niche on its base, each office adding one shape, so they
    // stay apart at 24px. Our own; the cog is the logo's.
    // Supplicant: the empty niche on its base
    public const string SupplicantInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/></svg>""";

    // Initiate: the cursor, “_”
    public const string InitiateInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/><path d="M10.6 17.4H13.4" stroke-width="1.8"/></svg>""";

    // Acolyte: the prompt, “>_”
    public const string AcolyteInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/><path d="M9.4 12.7 11.7 15 9.4 17.3" stroke-width="1.7"/><path d="M12.7 17.6H15" stroke-width="1.7"/></svg>""";

    // Lector: the cog on the apex
    public const string LectorInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/><path d="M9.4 12.7 11.7 15 9.4 17.3" stroke-width="1.7"/><path d="M12.7 17.6H15" stroke-width="1.7"/><path fill="currentColor" fill-rule="evenodd" stroke="none" d="M11.65 0.78L11.51 0.10L12.49 0.10L12.35 0.78L13.00 1.05L13.00 1.05L13.39 0.47L14.08 1.16L13.50 1.55L13.77 2.20L13.77 2.20L14.45 2.06L14.45 3.04L13.77 2.90L13.50 3.55L13.50 3.55L14.08 3.94L13.39 4.63L13.00 4.05L12.35 4.32L12.35 4.32L12.49 5.00L11.51 5.00L11.65 4.32L11.00 4.05L11.00 4.05L10.61 4.63L9.92 3.94L10.50 3.55L10.23 2.90L10.23 2.90L9.55 3.04L9.55 2.06L10.23 2.20L10.50 1.55L10.50 1.55L9.92 1.16L10.61 0.47L11.00 1.05L11.65 0.78ZM12.80 2.55A0.8 0.8 0 1 0 11.20 2.55A0.8 0.8 0 1 0 12.80 2.55Z"/></svg>""";

    // Keeper of Rites: a plinth under the base
    public const string KeeperOfRitesInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/><path d="M9.4 12.7 11.7 15 9.4 17.3" stroke-width="1.7"/><path d="M12.7 17.6H15" stroke-width="1.7"/><path fill="currentColor" fill-rule="evenodd" stroke="none" d="M11.65 0.78L11.51 0.10L12.49 0.10L12.35 0.78L13.00 1.05L13.00 1.05L13.39 0.47L14.08 1.16L13.50 1.55L13.77 2.20L13.77 2.20L14.45 2.06L14.45 3.04L13.77 2.90L13.50 3.55L13.50 3.55L14.08 3.94L13.39 4.63L13.00 4.05L12.35 4.32L12.35 4.32L12.49 5.00L11.51 5.00L11.65 4.32L11.00 4.05L11.00 4.05L10.61 4.63L9.92 3.94L10.50 3.55L10.23 2.90L10.23 2.90L9.55 3.04L9.55 2.06L10.23 2.20L10.50 1.55L10.50 1.55L9.92 1.16L10.61 0.47L11.00 1.05L11.65 0.78ZM12.80 2.55A0.8 0.8 0 1 0 11.20 2.55A0.8 0.8 0 1 0 12.80 2.55Z"/><path d="M3.5 22.6H20.5"/></svg>""";

    // Archivist: two pinnacles
    public const string ArchivistInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M7 20.5V12.5C7 9.3 9 6.9 12 5.2C15 6.9 17 9.3 17 12.5V20.5"/><path d="M5 20.5H19"/><path d="M9.4 12.7 11.7 15 9.4 17.3" stroke-width="1.7"/><path d="M12.7 17.6H15" stroke-width="1.7"/><path fill="currentColor" fill-rule="evenodd" stroke="none" d="M11.65 0.78L11.51 0.10L12.49 0.10L12.35 0.78L13.00 1.05L13.00 1.05L13.39 0.47L14.08 1.16L13.50 1.55L13.77 2.20L13.77 2.20L14.45 2.06L14.45 3.04L13.77 2.90L13.50 3.55L13.50 3.55L14.08 3.94L13.39 4.63L13.00 4.05L12.35 4.32L12.35 4.32L12.49 5.00L11.51 5.00L11.65 4.32L11.00 4.05L11.00 4.05L10.61 4.63L9.92 3.94L10.50 3.55L10.23 2.90L10.23 2.90L9.55 3.04L9.55 2.06L10.23 2.20L10.50 1.55L10.50 1.55L9.92 1.16L10.61 0.47L11.00 1.05L11.65 0.78ZM12.80 2.55A0.8 0.8 0 1 0 11.20 2.55A0.8 0.8 0 1 0 12.80 2.55Z"/><path d="M3.5 22.6H20.5"/><path d="M3.2 20.5V13.6M20.8 20.5V13.6"/><path d="M3.2 10.4 4.3 12.2H2.1zM20.8 10.4 21.9 12.2H19.7z" fill="currentColor" stroke-width="1"/></svg>""";

    // Elder of the Shrine: the niche filled solid, with “>_” cut out
    public const string ElderOfTheShrineInsignia =
        """<svg class="insignia" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path fill-rule="evenodd" fill="currentColor" stroke="none" d="M6 20.5V12.5C6 8.8 8.4 6 12 4C15.6 6 18 8.8 18 12.5V20.5ZM8.8 12.9 9.9 11.8 13.1 15 9.9 18.2 8.8 17.1 10.9 15ZM12.6 16.9H15.6V18.5H12.6Z"/><path d="M5 20.5H19"/><path fill="currentColor" fill-rule="evenodd" stroke="none" d="M11.65 0.78L11.51 0.10L12.49 0.10L12.35 0.78L13.00 1.05L13.00 1.05L13.39 0.47L14.08 1.16L13.50 1.55L13.77 2.20L13.77 2.20L14.45 2.06L14.45 3.04L13.77 2.90L13.50 3.55L13.50 3.55L14.08 3.94L13.39 4.63L13.00 4.05L12.35 4.32L12.35 4.32L12.49 5.00L11.51 5.00L11.65 4.32L11.00 4.05L11.00 4.05L10.61 4.63L9.92 3.94L10.50 3.55L10.23 2.90L10.23 2.90L9.55 3.04L9.55 2.06L10.23 2.20L10.50 1.55L10.50 1.55L9.92 1.16L10.61 0.47L11.00 1.05L11.65 0.78ZM12.80 2.55A0.8 0.8 0 1 0 11.20 2.55A0.8 0.8 0 1 0 12.80 2.55Z"/><path d="M3.5 22.6H20.5"/><path d="M3.2 20.5V13.6M20.8 20.5V13.6"/><path d="M3.2 10.4 4.3 12.2H2.1zM20.8 10.4 21.9 12.2H19.7z" fill="currentColor" stroke-width="1"/></svg>""";
}
