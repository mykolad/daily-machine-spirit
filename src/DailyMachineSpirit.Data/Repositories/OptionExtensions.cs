using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

internal static class OptionExtensions
{
    /// <summary>For the JSON documents only, which store a missing value as null.</summary>
    public static T? OrNull<T>(this Option<T> option) where T : class => option.MatchUnsafe(value => value, () => null);

    public static T? OrNullable<T>(this Option<T> option) where T : struct => option.MatchUnsafe(value => (T?)value, () => null);
}
