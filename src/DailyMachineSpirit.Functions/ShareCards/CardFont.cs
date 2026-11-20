using System.Text;
using SkiaSharp;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>
/// A share card's font at one size, with Noto Emoji for any character it lacks: an erasure's emoji
/// (<c>LostToTime</c>), anywhere in a title, a text or code. Skia draws only with the typeface it's given, so the text is
/// split where the font changes, and measured the same way it's drawn.
/// </summary>
public sealed class CardFont : IDisposable
{
    // An emoji's variation selector stays with the character before it.
    private const int VariationSelector = 0xFE0F;

    private readonly SKFont main;
    private readonly SKFont emoji;

    public CardFont(SKTypeface typeface, float size)
    {
        main = Font(typeface, size);
        emoji = Font(CardAssets.Emoji, size);
    }

    public float Size => main.Size;

    /// <summary>The main font's metrics, which place the lines: an emoji sits on the same baseline.</summary>
    public SKFontMetrics Metrics => main.Metrics;

    public float Measure(string text) => Pieces(text).Sum(piece => FontOf(piece).MeasureText(piece.Text));

    /// <summary>Draws the text from x on the baseline, and answers where it ends.</summary>
    public float Draw(SKCanvas canvas, string text, float x, float baseline, SKPaint paint)
    {
        foreach (var piece in Pieces(text))
        {
            var font = FontOf(piece);
            canvas.DrawText(piece.Text, x, baseline, font, paint);
            x += font.MeasureText(piece.Text);
        }
        return x;
    }

    public void Dispose()
    {
        main.Dispose();
        emoji.Dispose();
    }

    private SKFont FontOf((string Text, bool IsEmoji) piece) => piece.IsEmoji ? emoji : main;

    // The text in stretches of one font: the main font's, or the emoji font's where the main one has no glyph.
    private List<(string Text, bool IsEmoji)> Pieces(string text)
    {
        var pieces = new List<(string Text, bool IsEmoji)>();
        var current = new StringBuilder();
        var currentIsEmoji = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var isEmoji = rune.Value == VariationSelector
                ? currentIsEmoji
                : !main.Typeface.ContainsGlyph(rune.Value) && emoji.Typeface.ContainsGlyph(rune.Value);
            if (current.Length > 0 && isEmoji != currentIsEmoji)
            {
                pieces.Add((current.ToString(), currentIsEmoji));
                current.Clear();
            }
            currentIsEmoji = isEmoji;
            current.Append(rune.ToString());
        }
        if (current.Length > 0)
        {
            pieces.Add((current.ToString(), currentIsEmoji));
        }
        return pieces;
    }

    private static SKFont Font(SKTypeface typeface, float size)
        => new(typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
}
