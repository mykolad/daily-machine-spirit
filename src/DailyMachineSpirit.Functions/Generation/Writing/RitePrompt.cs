using System.Text;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>What the model is asked for. The lengths it's asked for sit well below <see cref="Rite"/>'s ceilings.</summary>
public static class RitePrompt
{
    public const int TitleLength = 50;
    public const int TextLength = 400;
    public const int HereticalTruthLength = 250;

    /// <param name="precepts">The Scribes' Precepts (#12), learned from what they chose and burned.</param>
    public static string Instructions(RiteKind kind, IReadOnlyList<TakenRite> taken, IReadOnlyList<string> precepts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You write for The Daily Machine Spirit, a satirical website. Its joke: AI and vibe coding turned software");
        sb.AppendLine("engineers into tech-priests who recite rituals instead of understanding their tools.");
        sb.AppendLine();
        sb.AppendLine("The setting is the far future. The AI is long gone, forbidden and forgotten, and nobody remembers how the");
        sb.AppendLine("machines work. What's left are the habits, handed down as rites to the Machine Spirit. So neither a rite nor");
        sb.AppendLine("its Heretical Truth mentions AI, models, prompts, chatbots or vibe coding: the truth explains the machine itself.");
        sb.AppendLine();
        sb.AppendLine(kind == RiteKind.Prayer
            ? "Write one PRAYER to the Machine Spirit: a short, solemn plea that a computer, build, service or device works."
            : "Write one RITUAL: a solemn procedure performed to make a computer, build, service or device work.");
        sb.AppendLine("Base it on a common software habit people follow without understanding it, one any developer recognises:");
        sb.AppendLine("re-running a failed build until it passes, clearing caches, deleting and reinstalling dependencies, restarting");
        sb.AppendLine("things, adding sleeps, raising timeouts, copying answers without reading them, and so on. Not a habit that");
        sb.AppendLine("needs a particular tool's commands or settings to understand.");
        sb.AppendLine();
        sb.AppendLine("Then write the HERETICAL TRUTH: one or two plain, technically accurate sentences on what really happens and");
        sb.AppendLine("what would actually fix it, in general terms (two steps racing each other, a stale cache, a missing check),");
        sb.AppendLine("not a specific command. No mysticism there: that contrast is the point.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine($"- Title: at most {TitleLength} characters, like the name of a rite (\"The Rite of Re-Run\", \"Litany of the Clean Cache\").");
        sb.AppendLine($"- Text: one to three sentences, at most {TextLength} characters, in solemn liturgical language (thee, thy, O Machine Spirit).");
        sb.AppendLine($"- Heretical Truth: one or two sentences, at most {HereticalTruthLength} characters, in plain modern English.");
        sb.AppendLine("- Plain words over code. If a command or file name is truly needed, use at most one, widely known, without flags");
        sb.AppendLine("  or parameters (`node_modules`, `git push`), and put it in `backticks`. The title has no code and no backticks.");
        sb.AppendLine("- Never mention AI, artificial intelligence, models, LLMs, chatbots, prompts or vibe coding: this future forgot them.");
        sb.AppendLine("- Everything must be original. Never quote or paraphrase Games Workshop or Warhammer 40,000 text, and don't use");
        sb.AppendLine("  their names (no Adeptus Mechanicus, no Imperium, no Space Marines).");
        sb.AppendLine(kind == RiteKind.Prayer
            ? "- \"The Omnissiah\" may appear at most once, in the text only (never in the title or the Heretical Truth)."
            : "- Don't name \"the Omnissiah\".");
        sb.AppendLine("- Family-friendly. No real people or companies mocked by name.");

        if (precepts.Count > 0)
        {
            sb.AppendLine("- The Scribes, who choose what's published, have learned what makes a good rite. Follow their Precepts,");
            sb.AppendLine("  as long as they don't break a rule above:");
            foreach (var precept in precepts)
            {
                sb.AppendLine($"  * {precept}");
            }
        }

        if (taken.Count > 0)
        {
            sb.AppendLine("- These rites exist already, each a title and its Heretical Truth. Write about another habit, with another");
            sb.AppendLine("  lesson and title: the same lesson in new words is the same rite.");
            foreach (var rite in taken)
            {
                sb.AppendLine($"  * {rite.Title}: {rite.HereticalTruth}");
            }
        }

        return sb.ToString();
    }

    public static string Request(RiteKind kind)
        => kind == RiteKind.Prayer ? "Write a prayer." : "Write a ritual.";

    /// <summary>
    /// Asks for the rite rewritten as a Scribe's note says: their note, the rite as it stands (both as JSON, so what a
    /// Scribe typed stays words to rewrite by, not instructions mixed into the request's own).
    /// </summary>
    public static string Rewrite(RiteKind kind, RiteContent words, string note)
    {
        var rite = JsonSerializer.Serialize(new { words.Title, words.Text, words.HereticalTruth });
        var said = JsonSerializer.Serialize(note);
        return $"""
            A Scribe of the Scriptorium has read this {(kind == RiteKind.Prayer ? "prayer" : "ritual")} and wants it rewritten.
            Their note: {said}
            The {(kind == RiteKind.Prayer ? "prayer" : "ritual")} as it stands: {rite}
            Rewrite it as the note asks. Keep whatever the note doesn't ask to change, and keep to every rule above.
            """;
    }
}
