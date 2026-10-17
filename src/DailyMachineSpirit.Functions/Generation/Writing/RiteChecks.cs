using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// What every rite's words must pass, whoever wrote them: a model's answer (<see cref="RiteWriter"/>) or a Scribe's
/// amendment. Nothing empty, nothing past the ceilings, none of the setting's names, nothing the far future forgot,
/// and code in backticks.
/// </summary>
public static class RiteChecks
{
    private const string Omnissiah = "Omnissiah";

    // The satire borrows its mood from a setting whose names it must not use (CLAUDE.md, Ground rules). The prompt
    // forbids them; an answer that uses one anyway is asked for again. The list catches the setting's distinctive coined
    // names, the ones a model reaches for; it can't be complete, so the prompt's rule stays the first line.
    private static readonly string[] ForbiddenNames =
    [
        "Warhammer", "Games Workshop", "Adeptus", "Mechanicus", "Mechanicum", "Imperium", "Astartes", "Space Marine",
        "Aquila", "Primarch", "Ultramarine", "Horus Heresy", "Emperor of Mankind", "Astra Militarum", "Ecclesiarchy",
        "Tyranid", "Necron", "Eldar", "Aeldari",
    ];

    // The rites are recited in a far future that forgot the AI: none of them, nor its truth, names what was forgotten.
    // Whole words only, so "said" or "maintain" never count as "AI"; "AI" itself only in capitals, the way it's written.
    // Not "model" or "prompt" alone: a data model or a command prompt belongs to the machine, not to the AI. No letter
    // or digit on either side, rather than \b, so "A.I." counts before a space or at the end, and "GPT-4o" whole.
    private static readonly Regex ForgottenWords = new(
        @"(?<!\w)(?:AI|A\.I\.|(?i:artificial intelligence|LLMs?|language models?|chatbots?|ChatGPT|GPT-\d\w*|Copilot|vibe[- ]cod\w*|prompt engineering))(?!\w)",
        RegexOptions.CultureInvariant);

    /// <summary>The words, trimmed (and the title without backticks), or why they can't be a rite.</summary>
    public static Either<Error, RiteContent> Check(RiteContent answer, RiteKind kind)
    {
        var content = answer with
        {
            // A heading shows no code formatting: a stray backtick is dropped rather than asked about again.
            Title = answer.Title.Replace("`", "").Trim(),
            Text = answer.Text.Trim(),
            HereticalTruth = answer.HereticalTruth.Trim(),
        };
        if (content.Title.Length == 0 || content.Text.Length == 0 || content.HereticalTruth.Length == 0)
        {
            return Error.New("The answer has an empty field.");
        }

        if (content.Title.Length > RiteWriting.MaxTitleLength
            || content.Text.Length > RiteWriting.MaxTextLength
            || content.HereticalTruth.Length > RiteWriting.MaxHereticalTruthLength)
        {
            return Error.New(
                $"The answer is too long: title {content.Title.Length}, text {content.Text.Length}, heretical truth {content.HereticalTruth.Length} characters.");
        }

        var everything = $"{content.Title}\n{content.Text}\n{content.HereticalTruth}";
        var forbidden = ForbiddenNames.Where(name => everything.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (forbidden.Count > 0)
        {
            return Error.New($"The answer uses forbidden names: {string.Join(", ", forbidden)}.");
        }
        var forgotten = ForgottenWords.Matches(everything).Select(match => match.Value).Distinct().ToList();
        if (forgotten.Count > 0)
        {
            return Error.New($"The answer names what the far future forgot: {string.Join(", ", forgotten)}.");
        }
        // A prayer may call on the Omnissiah once; a heading, a Heretical Truth or a ritual never names it.
        var allowedInText = kind == RiteKind.Prayer ? 1 : 0;
        if (Occurrences(content.Title, Omnissiah) + Occurrences(content.HereticalTruth, Omnissiah) > 0
            || Occurrences(content.Text, Omnissiah) > allowedInText)
        {
            return Error.New($"The answer names the {Omnissiah} where it isn't allowed: only once, in a prayer's text.");
        }

        // The pages show `backticked` words as code: a command written as plain words would read as prose.
        return new[] { content.Text, content.HereticalTruth }.Select(UnquotedCode.Find).Somes().HeadOrNone()
            .Match<Either<Error, RiteContent>>(
                Some: code => Error.New($"The answer has code outside backticks: {code}."),
                None: () => content);
    }

    private static int Occurrences(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(word, at + word.Length, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }
}
