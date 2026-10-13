using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

public sealed record Rite
{
    /// <summary>Given when the rite is saved; the page shows it ("A RITUAL · NO. 214") and links to it.</summary>
    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteWriting Writing { get; init; } = new();

    public int BlessedCount { get; init; }

    public int HeresyCount { get; init; }

    /// <summary>For "More rites"; added after the rite is saved.</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    /// <summary>
    /// What it's about, as tag keys from the site's vocabulary (the Functions app's <c>RiteTags</c>), given by its judge
    /// with the scores. None until then; <c>[]</c> when the judge gave none.
    /// </summary>
    public Option<string[]> Tags { get; init; }
}
