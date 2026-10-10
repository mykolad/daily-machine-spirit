namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The design's original artwork (design handoff), inline: decorative, so hidden from assistive technology.</summary>
public static class Svg
{
    private const string CogPaths =
        """<path fill="#c9a25a" fill-rule="evenodd" d="M27.71 10.42L28.14 3.26A29 29 0 0 1 35.86 3.26L36.29 10.42A22 22 0 0 1 44.22 13.71L49.59 8.95A29 29 0 0 1 55.05 14.41L50.29 19.78A22 22 0 0 1 53.58 27.71L60.74 28.14A29 29 0 0 1 60.74 35.86L53.58 36.29A22 22 0 0 1 50.29 44.22L55.05 49.59A29 29 0 0 1 49.59 55.05L44.22 50.29A22 22 0 0 1 36.29 53.58L35.86 60.74A29 29 0 0 1 28.14 60.74L27.71 53.58A22 22 0 0 1 19.78 50.29L14.41 55.05A29 29 0 0 1 8.95 49.59L13.71 44.22A22 22 0 0 1 10.42 36.29L3.26 35.86A29 29 0 0 1 3.26 28.14L10.42 27.71A22 22 0 0 1 13.71 19.78L8.95 14.41A29 29 0 0 1 14.41 8.95L19.78 13.71A22 22 0 0 1 27.71 10.42ZM46 32A14 14 0 1 0 18 32A14 14 0 1 0 46 32Z"></path><path d="M24.5 26.5l5.5 5.5-5.5 5.5M32.5 38.5h7" fill="none" stroke="#c9a25a" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round"></path>""";

    private const string WaxSeal =
        """<svg viewBox="0 0 64 64" width="92" height="92"><circle cx="32" cy="32" r="27" fill="#8f2a20"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="#8f2a20" stroke-width="5" stroke-dasharray="4 5" stroke-linecap="round"></circle><circle cx="32" cy="32" r="19" fill="none" stroke="#6b1c14" stroke-width="2.5"></circle><text x="32" y="38.5" text-anchor="middle" font-family="JetBrains Mono, monospace" font-size="17" font-weight="500" fill="#5c140e">{ }</text><ellipse cx="22" cy="19" rx="7" ry="3" fill="#b8473a" opacity=".6" transform="rotate(-35 22 19)"></ellipse></svg>""";

    private const string Corner =
        """<path d="M2 58V14L14 2h44" stroke="#a88a55" stroke-width="1.5"></path><path d="M10 40V18l8-8h22" stroke="#a88a55" stroke-width="1.5"></path><circle cx="40" cy="10" r="2.4" fill="#a88a55"></circle><circle cx="10" cy="40" r="2.4" fill="#a88a55"></circle>""";

    private const string Flame =
        """<ellipse cx="10" cy="9" rx="4" ry="7.5" fill="#f0b45a"></ellipse><rect x="4" y="18" width="12" height="26" rx="3" fill="#e9dcbc"></rect>""";

    public const string BrandCog = $"""<svg viewBox="0 0 64 64" width="28" height="28" aria-hidden="true">{CogPaths}</svg>""";

    public const string MastheadRule =
        $"""<svg class="masthead-rule" viewBox="0 0 320 40" width="320" height="40" fill="none" aria-hidden="true"><path d="M4 20h104l8-8h18M316 20H212l-8-8h-18" stroke="#84662c" stroke-width="1.5"></path><path d="M60 20l8 8h40M260 20l-8 8h-40" stroke="#84662c" stroke-width="1.5"></path><circle cx="4" cy="20" r="2.5" fill="#84662c"></circle><circle cx="316" cy="20" r="2.5" fill="#84662c"></circle><svg x="140" y="0" width="40" height="40" viewBox="0 0 64 64">{CogPaths}</svg></svg>""";

    public const string Corners =
        $"""<svg class="corner corner-tl" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-tr" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-bl" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg><svg class="corner corner-br" viewBox="0 0 60 60" width="60" height="60" fill="none" aria-hidden="true">{Corner}</svg>""";

    public const string RubricDivider =
        """<svg class="divider" viewBox="0 0 120 12" width="120" height="12" fill="none" aria-hidden="true"><path d="M0 6h46M74 6h46" stroke="#8f2a20" stroke-width="1.2"></path><path d="M60 0l6 6-6 6-6-6z" fill="#8f2a20"></path></svg>""";

    public const string BrassDivider =
        """<svg class="divider" viewBox="0 0 120 12" width="120" height="12" fill="none" aria-hidden="true"><path d="M0 6h46M74 6h46" stroke="#84662c" stroke-width="1.2"></path><path d="M60 0l6 6-6 6-6-6z" fill="#84662c"></path></svg>""";

    public const string PuritySeal =
        """<svg viewBox="0 0 64 64" width="40" height="40"><circle cx="32" cy="32" r="27" fill="#8f2a20"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="#8f2a20" stroke-width="5" stroke-dasharray="4 5" stroke-linecap="round"></circle><circle cx="32" cy="32" r="19" fill="none" stroke="#6b1c14" stroke-width="2.5"></circle><path d="M27 26l6 6-6 6M35 39h6" stroke="#5c140e" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round" fill="none"></path></svg>""";

    /// <summary>The seal twice, each copy clipped to one side of the crack (<see cref="Styles"/> moves the halves apart).</summary>
    public const string BreakableSeal =
        $"""<span class="seal-wax" aria-hidden="true"><span class="seal-half seal-left">{WaxSeal}</span><span class="seal-half seal-right">{WaxSeal}</span></span>""";

    public const string CandleLeft = $"""<svg class="candle" viewBox="0 0 20 44" width="20" height="44" fill="none" aria-hidden="true">{Flame}</svg>""";

    public const string CandleRight = $"""<svg class="candle candle-late" viewBox="0 0 20 44" width="20" height="44" fill="none" aria-hidden="true">{Flame}</svg>""";

    public const string SleepingSkull =
        """<svg viewBox="0 0 96 96" width="96" height="96" fill="none" aria-hidden="true"><path d="M20 46a28 28 0 0 1 56 0" stroke="#84662c" stroke-width="4"></path><path d="M48 22c-14 0-24 10-24 23 0 8 4 13 9 16v9a3 3 0 0 0 3 3h24a3 3 0 0 0 3-3v-9c5-3 9-8 9-16 0-13-10-23-24-23z" fill="#b6a98f"></path><path d="M34 48h10M52 48h10" stroke="#100e0b" stroke-width="3.5" stroke-linecap="round"></path><path d="M48 54l-3 5h6z" fill="#100e0b"></path><path d="M42 66v6M48 66v6M54 66v6" stroke="#100e0b" stroke-width="2"></path><rect x="14" y="42" width="10" height="18" rx="5" fill="#84662c"></rect><rect x="72" y="42" width="10" height="18" rx="5" fill="#84662c"></rect><text x="74" y="22" font-family="JetBrains Mono, monospace" font-size="12" fill="#84662c">z z</text></svg>""";

    public const string AwakeSkull =
        """<svg viewBox="0 0 96 96" width="96" height="96" fill="none" aria-hidden="true"><path d="M20 46a28 28 0 0 1 56 0" stroke="#c9a25a" stroke-width="4"></path><path d="M48 22c-14 0-24 10-24 23 0 8 4 13 9 16v9a3 3 0 0 0 3 3h24a3 3 0 0 0 3-3v-9c5-3 9-8 9-16 0-13-10-23-24-23z" fill="#e9dcbc"></path><circle cx="39" cy="47" r="5.5" fill="#100e0b"></circle><circle cx="57" cy="47" r="5.5" fill="#100e0b"></circle><path d="M48 54l-3 5h6z" fill="#100e0b"></path><path d="M42 66v6M48 66v6M54 66v6" stroke="#100e0b" stroke-width="2"></path><rect x="14" y="42" width="10" height="18" rx="5" fill="#c9a25a"></rect><rect x="72" y="42" width="10" height="18" rx="5" fill="#c9a25a"></rect><path d="M19 60c0 10 8 16 20 16" stroke="#c9a25a" stroke-width="3" stroke-linecap="round"></path><circle cx="41" cy="76" r="3" fill="#c9a25a"></circle></svg>""";

    // Blessed and Heresy: outlined, and filled by the stylesheet when pressed, so the state isn't colour alone.
    public const string FlameIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"></path></svg>""";

    public const string SkullIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 22a1 1 0 0 0 1-1v-1a2 2 0 0 0 1.56-3.25 8 8 0 1 0-11.12 0A2 2 0 0 0 8 20v1a1 1 0 0 0 1 1z"></path><circle cx="9" cy="12" r="1"></circle><circle cx="15" cy="12" r="1"></circle><path d="m12.5 17-.5-1-.5 1h1z"></path></svg>""";

    public const string SmallFlameIcon =
        """<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"></path></svg>""";

    public const string SmallSkullIcon =
        """<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 22a1 1 0 0 0 1-1v-1a2 2 0 0 0 1.56-3.25 8 8 0 1 0-11.12 0A2 2 0 0 0 8 20v1a1 1 0 0 0 1 1z"></path><circle cx="9" cy="12" r="1"></circle><circle cx="15" cy="12" r="1"></circle></svg>""";

    public const string LinkIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path></svg>""";

    public const string CopyIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="13" height="13" x="9" y="9" rx="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>""";

    public const string BackIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m12 19-7-7 7-7"></path><path d="M19 12H5"></path></svg>""";

    public const string RefreshIcon =
        """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"></path><path d="M21 3v5h-5"></path><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"></path><path d="M8 16H3v5"></path></svg>""";
}
