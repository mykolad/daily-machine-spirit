using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>What a reader searches for. Never logged: it may say something about them.</summary>
public static class SearchWords
{
    public const int MaxLength = 100;

    public const int MaxWords = 6;

    // Quotes around a word mean nothing here: every word must appear anyway.
    private static readonly char[] Quotes = ['"', '`', '\'', '“', '”', '‘', '’'];

    /// <summary>What the reader typed, trimmed, and cut to <see cref="MaxLength"/> characters.</summary>
    public static string Typed(string? query)
    {
        var typed = (query ?? string.Empty).Trim();
        if (typed.Length <= MaxLength)
        {
            return typed;
        }

        // A cut between the two halves of a character outside the BMP (an emoji) would leave half of it.
        var cut = char.IsHighSurrogate(typed[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return typed[..cut].TrimEnd();
    }

    /// <summary>The words to look for, each once, at most <see cref="MaxWords"/>.</summary>
    public static List<string> From(string typed)
        => typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim(Quotes))
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxWords)
            .ToList();

    /// <summary>
    /// Whether every word is somewhere in the rite's title, text or Heretical Truth, ignoring case: what the Archive's
    /// search asks of the database, for rites already read.
    /// </summary>
    public static bool FoundIn(Rite rite, IReadOnlyList<string> words)
        => words.All(word => new[] { rite.Writing.Title, rite.Writing.Text, rite.Writing.HereticalTruth }
            .Any(part => part.Contains(word, StringComparison.OrdinalIgnoreCase)));
}
