using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// Writes a UTC time with all seven decimals, for a field that queries order by. Cosmos orders the strings, and the
/// serializer's own format drops trailing zeros, so <c>…:00Z</c> would sort after <c>…:00.5Z</c>, as if newer.
/// </summary>
internal sealed class SortableUtcConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Utc.From(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(Utc.From(value).ToString(Format, CultureInfo.InvariantCulture));
}
