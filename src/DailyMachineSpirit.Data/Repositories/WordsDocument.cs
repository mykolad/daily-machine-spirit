using System.Text.Json.Serialization;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The fields of any document that may hold a rite's words, and its version: what <see cref="ExpurgationRepository"/>
/// reads. Each is null where that kind of document doesn't have it.
/// </summary>
internal sealed record WordsDocument
{
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("_etag")]
    public string ETag { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Text { get; init; }

    public string? HereticalTruth { get; init; }

    /// <summary>Written from the words, so it may hold an erased one too.</summary>
    public string? Ailment { get; init; }

    public string? Detail { get; init; }

    public string? Resembles { get; init; }

    /// <summary>A Scribe's note on a decision, which may quote the rite.</summary>
    public string? Note { get; init; }

    public WordsKept ToWordsKept()
    {
        var fields = new Dictionary<string, string>();
        void Keep(string path, string? value)
        {
            if (value is not null)
            {
                fields[path] = value;
            }
        }
        Keep("/title", Title);
        Keep("/text", Text);
        Keep("/hereticalTruth", HereticalTruth);
        Keep("/ailment", Ailment);
        Keep("/detail", Detail);
        Keep("/resembles", Resembles);
        Keep("/note", Note);
        return new WordsKept(Id, ETag, fields);
    }
}
