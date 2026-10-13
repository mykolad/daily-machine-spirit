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
        return typed.Length <= MaxLength ? typed : typed[..MaxLength].TrimEnd();
    }

    /// <summary>The words to look for, each once, at most <see cref="MaxWords"/>.</summary>
    public static List<string> From(string typed)
        => typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim(Quotes))
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxWords)
            .ToList();
}
