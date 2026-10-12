using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

internal static class Utc
{
    // A UTC time is written with its "Z" and read back as UTC; a local time would be read back as local.
    public static DateTime From(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };

    public static RiteSimilarity From(RiteSimilarity similarity) => similarity with { CreatedAtUtc = From(similarity.CreatedAtUtc) };
}
