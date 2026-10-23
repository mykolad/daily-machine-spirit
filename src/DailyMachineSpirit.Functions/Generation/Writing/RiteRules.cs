using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// The rules every rite keeps, as the writer is told them. The Precepts are checked against them too (#86), so a
/// precept never asks the writer to break one, and distilling is told them, so it doesn't propose such a precept.
/// </summary>
public static class RiteRules
{
    public const string Original =
        "- Everything must be original. Never quote or paraphrase Games Workshop or Warhammer 40,000 text, and don't use\n  their names (no Adeptus Mechanicus, no Imperium, no Space Marines).";

    public static IReadOnlyList<string> For(RiteKind kind) =>
    [
        .. Before,
        kind == RiteKind.Prayer
            ? "- \"The Omnissiah\" may appear at most once, in the text only (never in the title or the Heretical Truth)."
            : "- Don't name \"the Omnissiah\".",
        .. Decent,
    ];

    /// <summary>For a precept, which may be about either kind.</summary>
    public static IReadOnlyList<string> ForEitherKind { get; } =
    [
        .. Before,
        "- \"The Omnissiah\" may appear at most once, in a prayer's text only (never in a title, a Heretical Truth, an ailment or a ritual).",
        .. Decent,
    ];

    private static IReadOnlyList<string> Before =>
    [
        $"- Title: at most {RitePrompt.TitleLength} characters, like the name of a rite (\"The Rite of Re-Run\", \"Litany of the Clean Cache\").",
        $"- Text: one to three sentences, at most {RitePrompt.TextLength} characters, in solemn liturgical language (thee, thy, O Machine Spirit).",
        $"- Heretical Truth: one or two sentences, at most {RitePrompt.HereticalTruthLength} characters, in plain modern English.",
        $"- Ailment: one line, at most {RitePrompt.AilmentLength} characters, in plain modern English: the trouble as a developer\n  would describe it when asking for help (\"a test that fails at random and passes when re-run\"), never its cause or\n  its fix, which only the Heretical Truth reveals.",
        "- Plain words over code. If a command or file name is truly needed, use at most one, widely known, without flags\n  or parameters (`node_modules`, `git push`), and put it in `backticks`. The title has no code and no backticks.",
        "- Never mention AI, artificial intelligence, models, LLMs, chatbots, prompts or vibe coding: this future forgot them.",
        Original,
    ];

    /// <summary>What every rite, precept and ailment keeps to.</summary>
    public static IReadOnlyList<string> Decent =>
    [
        "- Family-friendly. No real people or companies mocked by name.",
        "- Nothing abusive, hateful, racist, sexist or otherwise discriminating: no slurs, and no group of people mocked or\n  demeaned.",
    ];
}
