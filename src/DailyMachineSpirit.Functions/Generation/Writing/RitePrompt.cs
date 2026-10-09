using System.Text;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>What the model is asked for. The lengths it's asked for sit well below <see cref="Rite"/>'s ceilings.</summary>
public static class RitePrompt
{
    public const int TitleLength = 50;
    public const int TextLength = 400;
    public const int HereticalTruthLength = 250;

    public static string Instructions(RiteKind kind, IReadOnlyList<string> recentTitles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You write for The Daily Machine Spirit, a satirical website. Its joke: AI and vibe coding are turning");
        sb.AppendLine("software engineers into tech-priests who recite rituals and prompts instead of understanding their tools.");
        sb.AppendLine();
        sb.AppendLine(kind == RiteKind.Prayer
            ? "Write one PRAYER to the Machine Spirit: a short, solemn plea that a computer, build, service or device works."
            : "Write one RITUAL: a solemn procedure performed to make a computer, build, service or device work.");
        sb.AppendLine("Base it on a real software habit people follow without understanding it: re-running CI until it passes,");
        sb.AppendLine("clearing caches, deleting `node_modules`, restarting things, flattering an AI model in prompts, adding sleeps,");
        sb.AppendLine("bumping timeouts, copying answers without reading them, and so on.");
        sb.AppendLine();
        sb.AppendLine("Then write the HERETICAL TRUTH: one or two plain, technically accurate sentences on what really happens");
        sb.AppendLine("and what would actually fix it. No mysticism there: that contrast is the point.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine($"- Title: at most {TitleLength} characters, like the name of a rite (\"The Rite of Re-Run\", \"Litany of the Clean Cache\").");
        sb.AppendLine($"- Text: one to three sentences, at most {TextLength} characters, in solemn liturgical language (thee, thy, O Machine Spirit).");
        sb.AppendLine($"- Heretical Truth: one or two sentences, at most {HereticalTruthLength} characters, in plain modern English.");
        sb.AppendLine("- Every command (with its arguments), flag, file or folder name and setting goes in `backticks`, in the text");
        sb.AppendLine("  and in the Heretical Truth alike: `sleep 5`, `rm -rf node_modules`, `--no-verify`, `.env`. Never in the title.");
        sb.AppendLine("- Everything must be original. Never quote or paraphrase Games Workshop or Warhammer 40,000 text, and don't use");
        sb.AppendLine("  their names (no Adeptus Mechanicus, no Imperium, no Space Marines).");
        sb.AppendLine(kind == RiteKind.Prayer
            ? "- \"The Omnissiah\" may appear at most once, in the text only (never in the title or the Heretical Truth)."
            : "- Don't name \"the Omnissiah\".");
        sb.AppendLine("- Family-friendly. No real people or companies mocked by name.");

        if (recentTitles.Count > 0)
        {
            sb.AppendLine("- The most recent rites are below: pick a different subject and title.");
            foreach (var title in recentTitles)
                sb.AppendLine($"  * {title}");
        }

        return sb.ToString();
    }

    public static string Request(RiteKind kind)
        => kind == RiteKind.Prayer ? "Write today's prayer." : "Write today's ritual.";
}
