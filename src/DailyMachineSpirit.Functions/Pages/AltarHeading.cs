namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A list page's heading (the archive, a tag's page) on an altar: the glow behind it, and on a wide screen a candle in
/// each margin and the circuit traces beyond (<see cref="Styles.Altar"/>), as Today's rite has. Nothing is added to
/// read: the candles and traces are hidden from screen readers.
/// </summary>
public static class AltarHeading
{
    /// <param name="heading">The page's h1, as HTML.</param>
    /// <param name="subtitle">The line under it, as HTML.</param>
    public static string Render(string heading, string subtitle)
        => $"""
            <div class="altar-head">
            <div class="archive-head">
            <h1 class="archive-title">{heading}</h1>
            <p class="archive-subtitle">{subtitle}</p>
            <div class="archive-divider">{Svg.BrassDivider}</div>
            </div>
            {Svg.AltarCandles}
            {Svg.Traces}
            </div>
            """;
}
