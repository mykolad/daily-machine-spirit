using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>A <see cref="Precept"/> as stored in the Precepts' document.</summary>
internal sealed record PreceptDocument
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool Pinned { get; init; }

    // Still written, so a version of the app from before Origin reads whose words a precept is.
    public bool ByScribe { get; init; }

    // Null on a precept kept before Origin: its ByScribe tells a Scribe's words from a distilled one.
    public PreceptOrigin? Origin { get; init; }

    public string? By { get; init; }

    public static PreceptDocument From(Precept precept) => new()
    {
        Id = precept.Id,
        Text = precept.Text,
        Pinned = precept.Pinned,
        ByScribe = precept.ByScribe,
        Origin = precept.Origin,
        By = precept.By.Map(scribe => scribe.Value).OrNull(),
    };

    public Precept ToPrecept() => new()
    {
        Id = Id,
        Text = Text,
        Pinned = Pinned,
        Origin = Origin ?? (ByScribe ? PreceptOrigin.ScribesWords : PreceptOrigin.Distilled),
        By = Optional(By).Map(id => new ScribeId(id)),
    };
}
