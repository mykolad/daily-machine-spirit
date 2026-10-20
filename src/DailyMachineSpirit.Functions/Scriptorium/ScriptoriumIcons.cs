namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's button icons (design handoff, scriptorium-icons.md), inline: decorative, since every button keeps
/// its label. They're drawn in the button's text colour, so they keep its contrast. Lucide's
/// (<c>Pages/Lucide-LICENSE.txt</c>), except the sparkle, which is the design's own. The design has no icon for Let the
/// Augury Decide, so it gets Lucide's eye; amending a draft gets the note's pencil, and saving it a check.
/// </summary>
public static class ScriptoriumIcons
{
    private const string Open = """<svg class="icon" aria-hidden="true" focusable="false" viewBox="0 0 24 24">""";

    public const string Summon =
        $"""{Open}<path d="M11 3l1.8 4.7L17.5 9.5l-4.7 1.8L11 16l-1.8-4.7L4.5 9.5l4.7-1.8z"/><path d="M19 15v6"/><path d="M16 18h6"/></svg>""";

    public const string Anoint =
        $"""{Open}<path d="M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7z"/></svg>""";

    public const string Exalt = $"""{Open}<path d="M12 19V5"/><path d="M5 12l7-7 7 7"/></svg>""";

    public const string Humble = $"""{Open}<path d="M12 5v14"/><path d="M19 12l-7 7-7-7"/></svg>""";

    public const string Burn =
        $"""{Open}<path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"/></svg>""";

    public const string Restore =
        $"""{Open}<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>""";

    public const string Refresh =
        $"""{Open}<path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/></svg>""";

    public const string Augury = $"""{Open}<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg>""";

    public const string Note = $"""{Open}<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z"/></svg>""";

    public const string Save = $"""{Open}<path d="M20 6 9 17l-5-5"/></svg>""";

    // The Precepts (#12) have no design: Lucide's pin, trash and plus.
    public const string Pin =
        $"""{Open}<path d="M12 17v5"/><path d="M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z"/></svg>""";

    public const string Delete =
        $"""{Open}<path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>""";

    public const string Add = $"""{Open}<path d="M5 12h14"/><path d="M12 5v14"/></svg>""";

    // The calendar's rows (the Scriptorium redesign): Lucide's chevron-down, to open a rite, and undo-2, after a burn.
    public const string Chevron = $"""{Open}<path d="m6 9 6 6 6-6"/></svg>""";

    public const string Undo = $"""{Open}<path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11"/></svg>""";

    // Every rite at once: Lucide's chevrons-up-down to open them, chevrons-down-up to fold them.
    public const string OpenEvery = $"""{Open}<path d="m7 15 5 5 5-5"/><path d="m7 9 5-5 5 5"/></svg>""";

    public const string FoldEvery = $"""{Open}<path d="m7 20 5-5 5 5"/><path d="m7 4 5 5 5-5"/></svg>""";
}
