using System.Text;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>What the model is asked for. The lengths it's asked for sit well below <see cref="Rite"/>'s ceilings.</summary>
public static class RitePrompt
{
    public const int TitleLength = 50;
    public const int TextLength = 400;
    public const int HereticalTruthLength = 250;
    public const int AilmentLength = 120;

    /// <param name="variety">For a new rite, the form to take and the images to rest; None for a rewrite, which keeps its own.</param>
    /// <param name="precepts">The Scribes' Precepts (#12), learned from what they chose and burned.</param>
    public static string Instructions(RiteKind kind, Option<RiteVariety> variety, IReadOnlyList<TakenRite> taken, IReadOnlyList<string> precepts)
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
        variety.IfSome(asked => sb.AppendLine(
            $"Its form is the {asked.Form.Name.ToLowerInvariant()}: {asked.Form.Shape}. Its title names the form (\"{asked.Form.Name} of the …\", \"The {asked.Form.Name} for …\")."));
        sb.AppendLine("Base it on a common software habit people follow without understanding it, one any developer recognises:");
        sb.AppendLine("re-running a failed build until it passes, clearing caches, deleting and reinstalling dependencies, restarting");
        sb.AppendLine("things, adding sleeps, raising timeouts, copying answers without reading them, and so on. Not a habit that");
        sb.AppendLine("needs a particular tool's commands or settings to understand.");
        sb.AppendLine();
        sb.AppendLine("Then write the HERETICAL TRUTH: one or two plain, technically accurate sentences on what really happens and");
        sb.AppendLine("what would actually fix it, in general terms (two steps racing each other, a stale cache, a missing check),");
        sb.AppendLine("not a specific command. No mysticism there: that contrast is the point.");
        sb.AppendLine();
        sb.AppendLine("Then write two FALSE TRUTHS, which a reader is offered beside the real one to guess which is true. Each is a");
        sb.AppendLine("cause and fix a developer might really believe, for the same trouble, but wrong: in the real truth's length,");
        sb.AppendLine("tone and plain English, so only understanding tells them apart. Not jokes, not obviously absurd, and not the");
        sb.AppendLine("real truth reworded; the two unlike each other.");
        sb.AppendLine();
        sb.AppendLine("Last, write the AILMENT: the trouble the rite is for, in a line, as a developer would put it when asking for help.");
        sb.AppendLine("Never its cause or its fix: the Heretical Truth stays sealed until the reader asks for it, and the ailment is what");
        sb.AppendLine("a reader's own trouble is matched against.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        foreach (var rule in RiteRules.For(kind))
        {
            sb.AppendLine(rule);
        }

        variety.Filter(asked => asked.WornImages.Count > 0).IfSome(asked =>
        {
            sb.AppendLine($"- The recent rites lean on the same few images: {string.Join(", ", asked.WornImages)}. Rest them here: reach for");
            sb.AppendLine($"  others ({string.Join(", ", WornImages.Fresh)}), or none.");
        });

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
        var rite = JsonSerializer.Serialize(new { words.Title, words.Text, words.HereticalTruth, words.FirstFalseTruth, words.SecondFalseTruth, words.Ailment });
        var said = JsonSerializer.Serialize(note);
        return $"""
            A Scribe of the Scriptorium has read this {(kind == RiteKind.Prayer ? "prayer" : "ritual")} and wants it rewritten.
            Their note: {said}
            The {(kind == RiteKind.Prayer ? "prayer" : "ritual")} as it stands: {rite}
            Rewrite it as the note asks. Keep whatever the note doesn't ask to change, and keep to every rule above.
            """;
    }
}
