namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The reader's rank in their Breviary (#201): the office's insignia and name, and below the highest office, a brass
/// rule of the way to the next and how many rites it needs. The rule is for the eye; the words say it. Rendered twice,
/// in the left margin on a wide screen and as a band above the sheet on a narrow one, and the stylesheet shows one.
/// </summary>
public static class RankBlock
{
    /// <param name="judged">The rites the reader voted on or confessed to, each once.</param>
    /// <param name="band">The narrow screen's band, whose label has an id of its own.</param>
    public static string Of(int judged, bool band)
    {
        var rank = Ranks.Of(judged);
        var label = band ? "rank-label-band" : "rank-label";
        var progress = Ranks.After(rank).Match(
            Some: _ => $"""
                <span class="rank-rule" aria-hidden="true"><span class="rank-fill" style="width:{Ranks.Progress(judged)}%"></span><span class="rank-knob" style="left:{Ranks.Progress(judged)}%"></span></span>
                <p class="rank-next">{Ranks.ToNext(judged)}</p>
                """,
            None: () => $"""<p class="rank-top">{Ranks.ToNext(judged)}.</p>""");
        return $"""
            <section class="rank{(band ? " rank-band" : string.Empty)}" aria-labelledby="{label}">
            {rank.Insignia}
            <div class="rank-words">
            <p class="rank-label" id="{label}">Thy rank</p>
            <p class="rank-name">{rank.Name}</p>
            {progress}
            </div>
            </section>
            """;
    }
}
