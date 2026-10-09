using System.Text.RegularExpressions;
using LanguageExt;
using static LanguageExt.Prelude;

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
        var outside = Backticked().Replace(text, " ");
        var found = Code().Match(outside);
        return found.Success ? Some(found.Value.Trim()) : None;
    }

    [GeneratedRegex("`[^`]*`")]
    private static partial Regex Backticked();

    [GeneratedRegex("""
        (?<![\w-])--[a-z][a-z0-9-]*
        | \b[a-z][a-z0-9]*_[a-z0-9_]+\b
        | \b[\w-]+\.(?:json|ya?ml|toml|lock|env|js|ts|cs|py|sh|ini|config|xml)\b
        | (?<![\w.])\.(?:env|gitignore|npmrc|bashrc)\b
        | \bsleep\s+\d+
        | \brm\s+-\w+
        | \b(?:npm|yarn|pnpm)\s+(?:install|i|ci|run|cache|update)\b
        | \bgit\s+(?:push|pull|reset|rebase|commit|clean|stash|checkout|merge)\b
        | \b(?:docker|kubectl)\s+(?:run|restart|rm|delete|apply|build|compose)\b
        | \bdotnet\s+(?:clean|build|restore|test|run)\b
        | \bsudo\s+\w+
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    private static partial Regex Code();
}
