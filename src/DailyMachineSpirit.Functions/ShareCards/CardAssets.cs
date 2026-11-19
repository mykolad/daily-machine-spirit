using SkiaSharp;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>
/// The share cards' fonts and images, from the assembly: the fonts as static TTF files (Skia can't read the pages'
/// WOFF2), and the design's parchment and seal. Read once, when a card is first drawn.
/// </summary>
public static class CardAssets
{
    private const string Prefix = "DailyMachineSpirit.Functions.ShareCards.";

    private static readonly Lazy<SKTypeface> CinzelBold = new(() => Typeface("Fonts.Cinzel-Bold.ttf"));
    private static readonly Lazy<SKTypeface> GaramondRegular = new(() => Typeface("Fonts.EBGaramond-Regular.ttf"));
    private static readonly Lazy<SKTypeface> MonoRegular = new(() => Typeface("Fonts.JetBrainsMono-Regular.ttf"));
    private static readonly Lazy<SKTypeface> MonoMedium = new(() => Typeface("Fonts.JetBrainsMono-Medium.ttf"));
    private static readonly Lazy<SKTypeface> NotoEmoji = new(() => Typeface("Fonts.NotoEmoji.ttf"));
    private static readonly Lazy<SKImage> ParchmentImage = new(() => Image("Assets.parchment-1152x582.png"));
    private static readonly Lazy<SKImage> SealImage = new(() => Image("Assets.seal-176.png"));
    private static readonly Lazy<byte[]> SiteCardBytes = new(() => Bytes("Assets.card-site.png"));

    /// <summary>Cinzel 700: the kicker and the title.</summary>
    public static SKTypeface Cinzel => CinzelBold.Value;

    /// <summary>EB Garamond 400: the rite's text.</summary>
    public static SKTypeface Garamond => GaramondRegular.Value;

    /// <summary>JetBrains Mono 400: code, and the margins' hex.</summary>
    public static SKTypeface Mono => MonoRegular.Value;

    /// <summary>JetBrains Mono 500: the foot's date and address.</summary>
    public static SKTypeface MonoBold => MonoMedium.Value;

    /// <summary>Noto Emoji, in one colour: the characters the other fonts lack, an erasure's emoji (<see cref="CardFont"/>).</summary>
    public static SKTypeface Emoji => NotoEmoji.Value;

    /// <summary>The sheet's texture, 1152 × 582, its vignette baked in.</summary>
    public static SKImage Parchment => ParchmentImage.Value;

    /// <summary>The whole wax seal, 176 × 176, drawn at half that.</summary>
    public static SKImage Seal => SealImage.Value;

    /// <summary>The site's card, as the design rendered it: one file for every page that isn't a rite's.</summary>
    public static byte[] SiteCard => SiteCardBytes.Value;

    private static SKTypeface Typeface(string name)
        => SKTypeface.FromData(SKData.CreateCopy(Bytes(name)))
            ?? throw new InvalidOperationException($"The embedded font {name} couldn't be read.");

    private static SKImage Image(string name)
        => SKImage.FromEncodedData(Bytes(name))
            ?? throw new InvalidOperationException($"The embedded image {name} couldn't be read.");

    private static byte[] Bytes(string name)
    {
        using var stream = typeof(CardAssets).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The embedded file {name} is missing.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
