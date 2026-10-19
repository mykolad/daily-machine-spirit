namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>
/// What an erased word becomes: an emoji, as if time had eaten it. Each is one code point that shows as an emoji by
/// itself (no variation selector), so a word's place in the text holds one picture.
/// </summary>
public static class LostToTime
{
    public static readonly string[] Emojis = ["💀", "🦴", "🔥", "📜", "⏳", "⌛", "🌑", "🍂", "🐀", "🪦", "🧱", "🪨", "🦇", "🐛", "🌀"];

    public static string Pick(Random random) => Emojis[random.Next(Emojis.Length)];

    public static bool IsOne(string emoji) => Emojis.Contains(emoji, StringComparer.Ordinal);
}
