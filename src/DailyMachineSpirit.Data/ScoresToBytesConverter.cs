using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DailyMachineSpirit.Data;

/// <summary>
/// Stores <see cref="Entities.ItemProfile.Scores"/> as one varbinary: the floats one after another, 4 bytes each,
/// little-endian whatever the machine, so any host reads them back the same.
/// </summary>
internal sealed class ScoresToBytesConverter() : ValueConverter<float[], byte[]>(
    scores => ToBytes(scores),
    bytes => ToScores(bytes))
{
    private static byte[] ToBytes(float[] scores)
    {
        var bytes = new byte[scores.Length * sizeof(float)];
        for (var i = 0; i < scores.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), scores[i]);
        return bytes;
    }

    private static float[] ToScores(byte[] bytes)
    {
        var scores = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < scores.Length; i++)
            scores[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        return scores;
    }
}

/// <summary>
/// Arrays compare by reference, so without this EF would miss a score changed in place and not save it.
/// </summary>
internal sealed class ScoresComparer() : ValueComparer<float[]>(
    (a, b) => a!.SequenceEqual(b!),
    scores => scores.Aggregate(0, (hash, score) => HashCode.Combine(hash, score)),
    scores => scores.ToArray());
