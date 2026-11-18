using System.Text;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Small parchments, one per rite, each its kind, day and title alone: enough to pick one. Today's recent rites and a
/// rite's More rites, so the two read as the same thing. Each card is one link, its focus ring outside it on the dark
/// ground.
/// </summary>
public static class ParchmentCards
{
    /// <summary>The cards in a list; fewer than a row of four share the row evenly, rather than leaving a gap.</summary>
    public static string List(IReadOnlyList<Rite> rites)
    {
        var cards = new StringBuilder();
        foreach (var rite in rites)
        {
            cards.Append($"""
                <li><a class="parchment" href="{Paths.Rite(rite.Number)}">
                <span class="parchment-kicker">A {(rite.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")} · {ImperialDate.InALink(rite.PublishedOnUtc, "parchment-date")}</span>
                <span class="parchment-title">{Html.Encode(rite.Writing.Title)}</span>
                {Svg.SmallSeal}
                </a></li>
                """);
        }

        return $"""<ul class="{(rites.Count < 4 ? "parchments parchments-few" : "parchments")}">{cards}</ul>""";
    }
}
