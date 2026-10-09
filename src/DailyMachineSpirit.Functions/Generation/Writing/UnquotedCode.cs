using System.Text;
using System.Text.RegularExpressions;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// Finds code a model wrote as plain words: the pages show `backticked` text as code, so an unquoted <c>sleep 5</c> reads
/// as prose. Only unmistakable code counts (a command with its arguments, a flag, a snake_case name, a file name), so
/// liturgical words like "sleep" or "the cache" never send an answer back.
/// </summary>
public static partial class UnquotedCode
{
    /// <summary>The first piece of code outside backticks, if any.</summary>
    public static Option<string> Find(string text)
    {
        // Matched with the backticks taken out, so code only half in backticks (`sleep` 5) is still found whole; it
        // counts as quoted only when all of it is inside one pair.
        var plain = new StringBuilder(text.Length);
        var spanOf = new List<int>(text.Length);
        var spans = 0;
        var inside = false;
        foreach (var character in text)
        {
            if (character == '`')
            {
                inside = !inside;
                spans += inside ? 1 : 0;
                continue;
            }
            plain.Append(character);
            spanOf.Add(inside ? spans : 0);
        }
        // A backtick that's never closed shows as a backtick, not as code.
        if (inside)
            for (var at = spanOf.Count - 1; at >= 0 && spanOf[at] == spans; at--)
                spanOf[at] = 0;

        return Code().Matches(plain.ToString())
            .Where(found => !InOneSpan(spanOf, found))
            .Select(found => found.Value)
            .HeadOrNone();
    }

    private static bool InOneSpan(List<int> spanOf, Match found)
        => spanOf[found.Index] != 0
           && Enumerable.Range(found.Index, found.Length).All(at => spanOf[at] == spanOf[found.Index]);

    [GeneratedRegex("""
        (?<![\w-])--[a-z][a-z0-9-]*
        | \b[a-z][a-z0-9]*_[a-z0-9_]+\b
        | \b(?!(?:node|next|vue|react|express)\.js\b)[\w-]+\.(?:json|ya?ml|toml|lock|env|js|ts|cs|py|sh|ini|config|xml)\b
        | (?<![\w.])\.(?:env|gitignore|npmrc|bashrc)\b
        | \bsleep\s+\d+
        | \brm\s+-\w+
        | \b(?:npm|yarn|pnpm)\s+(?:install|i|ci|run|update)\b
        | \bgit\s+(?:push|pull|reset|rebase|commit|clean|stash|checkout|merge)\b
        | \b(?:docker|kubectl)\s+(?:run|restart|rm|delete|apply|build)\b
        | \bdotnet\s+(?:clean|build|restore|test|run)\b
        | \bsudo\s+\w+
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    private static partial Regex Code();
}
