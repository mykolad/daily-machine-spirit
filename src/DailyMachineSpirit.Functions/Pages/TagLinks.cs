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
        => string.Join(" ", tags.Select(tag => $"""<a class="tag" href="{Paths.Tag(tag.Key)}">{Label(tag, words)}</a>"""));

    // A marked label is held in one span: the link is a flex box, and a mark directly in it would be an item of its own,
    // the space beside it trimmed away ("Versioncontrol").
    private static string Label(Tag tag, IReadOnlyList<string> words)
    {
        var naming = words.Where(word => RiteTags.Names(tag, word)).ToList();
        return naming.Count == 0 ? Html.Encode(tag.Label) : $"<span>{Highlight.Of(tag.Label, naming)}</span>";
    }
}
