using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Pages;
using LanguageExt;
using SkiaSharp;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>
/// The rite as a parchment card, drawn with Skia to the design's numbers (docs/design/share-card.md): the
/// kicker, the title, the divider and the text centred in the content box, the date and address at the foot, and the
/// seal across the sheet's lower edge, always whole. Around the sheet the card is transparent but for its shadow, so a
/// chat's own background shows. Long texts take a size or two down, then the title alone.
/// </summary>
public sealed class RiteCards : IRiteCards
{
    public const int Width = 1200;
    public const int Height = 630;

    private const float ContentLeft = 96;
    private const float ContentWidth = 1008;
    private const float ContentTop = 60;
    private const float ContentHeight = 464;
    // The title alone sits a little above the middle, leaving the foot its room.
    private const float TitleOnlyHeight = 394;
    private const float KickerSize = 22;
    private const float KickerLine = 28;
    private const float TitleLeading = 1.08f;
    private const float TextLeading = 1.36f;
    private const float DividerWidth = 160;
    private const float DividerHeight = 16;
    private const float FootBaseline = 564;
    // The margins' code, as on the rite's page: "READ THE CODE" in hex, "KNOW" in binary.
    private const string LeftCode = "52 45 41 44 20 54 48 45 20 43 4F 44 45 · ";
    private const string RightCode = "01001011 01001110 01001111 01010111 · ";

    private static readonly float[] TextSizes = [48, 44, 40];

    private static readonly SKColor Rubric = SKColor.Parse("#8f2a20");
    private static readonly SKColor Ink = SKColor.Parse("#2b2117");
    private static readonly SKColor Faded = SKColor.Parse("#5c4a33");
    private static readonly SKColor Brass = SKColor.Parse("#a88a55");
    private static readonly SKColor HexInk = SKColor.Parse("#a88f62");
    private static readonly SKColor ChipFill = SKColor.Parse("#d8c59a");
    private static readonly SKColor Shadow = new(0, 0, 0, 71);

    private static readonly SKRect Sheet = new(24, 24, 1176, 606);
    private static readonly SKSamplingOptions Smooth = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public byte[] Draw(Rite rite, string host)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        DrawSheet(canvas);
        DrawContent(canvas, rite);
        DrawFoot(canvas, $"{ImperialDate.Of(rite.PublishedOnUtc)}", $"{host}{Paths.Rite(rite.Number)}");
        canvas.DrawImage(CardAssets.Seal, new SKRect(556, 538, 644, 626), Smooth);
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    // The parchment on its soft shadow, the double rule, the corners and the margins' code.
    private static void DrawSheet(SKCanvas canvas)
    {
        using (var shadow = new SKPaint { Color = Shadow, IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7) })
        {
            canvas.DrawRoundRect(new SKRect(Sheet.Left, Sheet.Top + 4, Sheet.Right, Sheet.Bottom + 4), 20, 20, shadow);
        }
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(Sheet, 20), SKClipOperation.Intersect, true);
        canvas.DrawImage(CardAssets.Parchment, Sheet, Smooth);
        canvas.Restore();

        Rule(canvas, new SKRect(36, 36, 1164, 594), 2, 12);
        Rule(canvas, new SKRect(42, 42, 1158, 588), 1, 8);
        Corner(canvas, 52, 52, false, false);
        Corner(canvas, 1104, 52, true, false);
        Corner(canvas, 52, 534, false, true);
        Corner(canvas, 1104, 534, true, true);
        MarginCode(canvas, LeftCode, 60);
        MarginCode(canvas, RightCode, 1140);
    }

    // A border drawn inside its box, as CSS draws one.
    private static void Rule(SKCanvas canvas, SKRect box, float width, float radius)
    {
        using var paint = new SKPaint { Color = Rubric, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };
        var inside = box;
        inside.Inflate(-width / 2, -width / 2);
        canvas.DrawRoundRect(inside, radius, radius, paint);
    }

    // The design's corner (60 units drawn at 44px), mirrored for the other three.
    private static void Corner(SKCanvas canvas, float x, float y, bool flipX, bool flipY)
    {
        const float scale = 44f / 60f;
        canvas.Save();
        canvas.Translate(flipX ? x + 44 : x, flipY ? y + 44 : y);
        canvas.Scale(flipX ? -scale : scale, flipY ? -scale : scale);
        using var line = new SKPaint { Color = Brass, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var outer = SKPath.ParseSvgPathData("M2 58V14L14 2h44");
        using var inner = SKPath.ParseSvgPathData("M10 40V18l8-8h22");
        canvas.DrawPath(outer, line);
        canvas.DrawPath(inner, line);
        using var dot = new SKPaint { Color = Brass, IsAntialias = true };
        canvas.DrawCircle(40, 10, 2.4f, dot);
        canvas.DrawCircle(10, 40, 2.4f, dot);
        canvas.Restore();
    }

    // Faint code running down a margin from y 134 to 496, centred on x, clipped where the column ends.
    private static void MarginCode(SKCanvas canvas, string code, float x)
    {
        const float top = 134;
        const float length = 496 - top;
        using var font = Font(CardAssets.Mono, 13);
        canvas.Save();
        canvas.Translate(x, top);
        canvas.RotateDegrees(90);
        canvas.ClipRect(new SKRect(0, -20, length, 20));
        var repeated = string.Concat(Enumerable.Repeat(code, 8));
        Spaced(canvas, repeated, 0, font.Metrics.CapHeight / 2, font, HexInk, 13 * 0.4f);
        canvas.Restore();
    }

    private static void DrawContent(SKCanvas canvas, Rite rite)
    {
        var kicker = $"A {(rite.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")} · NO. {rite.Number}";
        var titleWords = rite.Writing.Title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var textWords = CardText.Words(rite.Writing.Text);

        var (titleSize, titleLines) = TitleAt(titleWords, 56, 2).Match(
            Some: lines => (56f, lines),
            None: () => (48f, TitleAt(titleWords, 48, 3).IfNone(() => Greedy(titleWords, 48))));
        foreach (var textSize in TextSizes)
        {
            using var textFont = Font(CardAssets.Garamond, textSize);
            var lines = TextLines(textWords, textFont, textSize);
            var height = KickerLine + 10 + titleLines.Count * titleSize * TitleLeading + 16 + DividerHeight + 22 + lines.Count * textSize * TextLeading;
            if (height <= ContentHeight)
            {
                var top = ContentTop + (ContentHeight - height) / 2;
                var below = Heading(canvas, kicker, titleLines, titleSize, top);
                DrawText(canvas, lines, textFont, textSize, below + 22);
                return;
            }
        }

        // Too long at the smallest size: the title alone, larger, the text left to the page.
        var alone = TitleAt(titleWords, 64, 3).IfNone(() => Greedy(titleWords, 64));
        var aloneHeight = KickerLine + 10 + alone.Count * 64 * TitleLeading + 16 + DividerHeight;
        Heading(canvas, kicker, alone, 64, ContentTop + (TitleOnlyHeight - aloneHeight) / 2);
    }

    // The kicker, the title and the divider from the top given; answers where the divider ends.
    private static float Heading(SKCanvas canvas, string kicker, IReadOnlyList<string> titleLines, float titleSize, float top)
    {
        using var kickerFont = Font(CardAssets.Cinzel, KickerSize);
        var spacing = KickerSize * 0.22f;
        var kickerWidth = SpacedWidth(kicker, kickerFont, spacing);
        Spaced(canvas, kicker, Width / 2f - kickerWidth / 2, Baseline(kickerFont, top, KickerLine), kickerFont, Rubric, spacing);

        using var titleFont = Font(CardAssets.Cinzel, titleSize);
        using var titlePaint = new SKPaint { Color = Rubric, IsAntialias = true };
        var titleLine = titleSize * TitleLeading;
        var lineTop = top + KickerLine + 10;
        foreach (var line in titleLines)
        {
            canvas.DrawText(line, Width / 2f, Baseline(titleFont, lineTop, titleLine), SKTextAlign.Center, titleFont, titlePaint);
            lineTop += titleLine;
        }

        var dividerTop = lineTop + 16;
        Divider(canvas, dividerTop);
        return dividerTop + DividerHeight;
    }

    // The design's divider (120 × 12 units drawn at 160 × 16), centred.
    private static void Divider(SKCanvas canvas, float top)
    {
        const float scale = DividerWidth / 120f;
        canvas.Save();
        canvas.Translate(Width / 2f - DividerWidth / 2, top);
        canvas.Scale(scale, scale);
        using var line = new SKPaint { Color = Rubric, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
        using var rules = SKPath.ParseSvgPathData("M0 6h46M74 6h46");
        canvas.DrawPath(rules, line);
        using var fill = new SKPaint { Color = Rubric, IsAntialias = true };
        using var diamond = SKPath.ParseSvgPathData("M60 0l6 6-6 6-6-6z");
        canvas.DrawPath(diamond, fill);
        canvas.Restore();
    }

    private static void DrawText(SKCanvas canvas, IReadOnlyList<IReadOnlyList<IReadOnlyList<CardRun>>> lines, SKFont font, float size, float top)
    {
        using var ink = new SKPaint { Color = Ink, IsAntialias = true };
        using var codeFont = Font(CardAssets.Mono, size * 0.76f);
        using var chip = new SKPaint { Color = ChipFill, IsAntialias = true };
        var space = font.MeasureText(" ");
        var lineHeight = size * TextLeading;
        var lineTop = top;
        foreach (var line in lines)
        {
            var baseline = Baseline(font, lineTop, lineHeight);
            var x = ContentLeft;
            foreach (var word in line)
            {
                foreach (var run in word)
                {
                    if (!run.IsCode)
                    {
                        canvas.DrawText(run.Text, x, baseline, font, ink);
                        x += font.MeasureText(run.Text);
                        continue;
                    }
                    // The chip around the code, its padding in the code's own size, as the page's CSS has it.
                    var codeSize = codeFont.Size;
                    var width = CodeWidth(run.Text, codeFont);
                    var box = new SKRect(x, baseline + codeFont.Metrics.Ascent - 0.06f * codeSize, x + width, baseline + codeFont.Metrics.Descent + 0.1f * codeSize);
                    canvas.DrawRoundRect(box, 8, 8, chip);
                    canvas.DrawText(run.Text, x + 0.32f * codeSize, baseline, codeFont, ink);
                    x += width;
                }
                x += space;
            }
            lineTop += lineHeight;
        }
    }

    private static void DrawFoot(SKCanvas canvas, string date, string address)
    {
        using var font = Font(CardAssets.MonoBold, 22);
        using var paint = new SKPaint { Color = Faded, IsAntialias = true };
        canvas.DrawText(date, ContentLeft, FootBaseline, font, paint);
        // Right of the seal (to x 644, and a little room): a longer host than the site's own, as staging's, shrinks to fit.
        const float room = ContentLeft + ContentWidth - 660;
        var width = font.MeasureText(address);
        using var addressFont = Font(CardAssets.MonoBold, width <= room ? 22 : 22 * room / width);
        canvas.DrawText(address, ContentLeft + ContentWidth, FootBaseline, SKTextAlign.Right, addressFont, paint);
    }

    // The title in at most this many lines at this size; two lines are balanced, broken where they're closest in width.
    private static Option<IReadOnlyList<string>> TitleAt(string[] words, float size, int maxLines)
    {
        using var font = Font(CardAssets.Cinzel, size);
        var greedy = Greedy(words, font);
        if (greedy.Count > maxLines)
        {
            return Option<IReadOnlyList<string>>.None;
        }
        if (greedy.Count != 2)
        {
            return Option<IReadOnlyList<string>>.Some(greedy);
        }
        var best = Enumerable.Range(1, words.Length - 1)
            .Select(split => (First: string.Join(' ', words[..split]), Second: string.Join(' ', words[split..])))
            .Where(pair => font.MeasureText(pair.First) <= ContentWidth && font.MeasureText(pair.Second) <= ContentWidth)
            .MinBy(pair => Math.Abs(font.MeasureText(pair.First) - font.MeasureText(pair.Second)));
        return Option<IReadOnlyList<string>>.Some(new[] { best.First, best.Second });
    }

    private static IReadOnlyList<string> Greedy(string[] words, float size)
    {
        using var font = Font(CardAssets.Cinzel, size);
        return Greedy(words, font);
    }

    private static List<string> Greedy(string[] words, SKFont font)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in words)
        {
            var longer = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && font.MeasureText(longer) > ContentWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }
        if (line.Length > 0)
        {
            lines.Add(line);
        }
        return lines;
    }

    // The text broken greedily into lines of the content's width; a lone last word takes one more from the line before,
    // when the line before can give it.
    private static List<IReadOnlyList<IReadOnlyList<CardRun>>> TextLines(IReadOnlyList<IReadOnlyList<CardRun>> words, SKFont font, float size)
    {
        using var codeFont = Font(CardAssets.Mono, size * 0.76f);
        var space = font.MeasureText(" ");
        float WordWidth(IReadOnlyList<CardRun> word) => word.Sum(run => run.IsCode ? CodeWidth(run.Text, codeFont) : font.MeasureText(run.Text));
        float LineWidth(IReadOnlyList<IReadOnlyList<CardRun>> line) => line.Sum(WordWidth) + space * Math.Max(0, line.Count - 1);

        var lines = new List<List<IReadOnlyList<CardRun>>>();
        var current = new List<IReadOnlyList<CardRun>>();
        foreach (var word in words)
        {
            if (current.Count > 0 && LineWidth([.. current, word]) > ContentWidth)
            {
                lines.Add(current);
                current = [];
            }
            current.Add(word);
        }
        if (current.Count > 0)
        {
            lines.Add(current);
        }
        if (lines.Count >= 2 && lines[^1].Count == 1 && lines[^2].Count > 2)
        {
            var given = lines[^2][^1];
            if (LineWidth([given, .. lines[^1]]) <= ContentWidth)
            {
                lines[^2].RemoveAt(lines[^2].Count - 1);
                lines[^1].Insert(0, given);
            }
        }
        return [.. lines.Select(line => (IReadOnlyList<IReadOnlyList<CardRun>>)line)];
    }

    private static float CodeWidth(string code, SKFont codeFont) => codeFont.MeasureText(code) + 2 * 0.32f * codeFont.Size;

    // Text with letter spacing, a character at a time (Skia has none of its own).
    private static void Spaced(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKColor color, float spacing)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        foreach (var character in text.EnumerateRunes())
        {
            var glyph = character.ToString();
            canvas.DrawText(glyph, x, baseline, font, paint);
            x += font.MeasureText(glyph) + spacing;
        }
    }

    private static float SpacedWidth(string text, SKFont font, float spacing)
        => text.EnumerateRunes().Sum(character => font.MeasureText(character.ToString())) + spacing * (text.Length - 1);

    // Where a line's baseline falls in its line box, as CSS places it: the leading split above and below the glyphs.
    private static float Baseline(SKFont font, float lineTop, float lineHeight)
    {
        var ascent = -font.Metrics.Ascent;
        var descent = font.Metrics.Descent;
        return lineTop + (lineHeight - (ascent + descent)) / 2 + ascent;
    }

    private static SKFont Font(SKTypeface typeface, float size)
        => new(typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
}
