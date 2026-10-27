using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A rite's tags as links to their pages. They sit on the dark background, never on the parchment.</summary>
public static class TagLinks
{
    public static string Render(IEnumerable<Tag> tags) => Marked(tags, []);

    /// <summary>
    /// As <see cref="Render"/>, with the words searched for marked in the subjects they name, so a rite found by its
    /// subject alone shows why.
    /// </summary>
    public static string Marked(IEnumerable<Tag> tags, IReadOnlyList<string> words)
        => string.Join(" ", tags.Select(tag =>
            $"""<a class="tag" href="{Paths.Tag(tag.Key)}">{Highlight.Of(tag.Label, words.Where(word => RiteTags.Names(tag, word)).ToList())}</a>"""));
}
