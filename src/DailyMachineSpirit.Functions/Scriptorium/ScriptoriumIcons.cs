namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's button icons (design handoff, scriptorium-icons.md), inline: decorative, since every button keeps
/// its label. They're drawn in the button's text colour, so they keep its contrast. Lucide's
/// (<c>Pages/Lucide-LICENSE.txt</c>), except the sparkle, which is the design's own.
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

    public const string Note = $"""{Open}<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z"/></svg>""";
}
