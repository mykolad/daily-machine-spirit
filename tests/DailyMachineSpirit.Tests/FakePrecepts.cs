using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Precepts for a test without the Cosmos DB emulator: none, some, or unreadable, as when Cosmos is down.</summary>
public sealed class FakePrecepts : IPreceptRepository
{
    public Either<Error, StoredPrecepts> Stored { get; set; } = new StoredPrecepts(new PreceptBook(), None);

    /// <summary>What saving answers: a failure keeps the Precepts as they were.</summary>
    public Either<Error, Unit> Saving { get; set; } = unit;

    public void Hold(params string[] texts)
        => Stored = new StoredPrecepts(
            new PreceptBook { Precepts = texts.Select(text => new Precept { Id = Guid.NewGuid(), Text = text }).ToList() }, Some("etag"));

    public Task<Either<Error, StoredPrecepts>> Get(CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task<Either<Error, Unit>> Save(StoredPrecepts read, PreceptBook changed, AuditEntry audit, CancellationToken cancellationToken)
    {
        Saving.IfRight(_ => Stored = new StoredPrecepts(changed, Some("etag")));
        return Task.FromResult(Saving);
    }
}
