using System.Text.Json.Nodes;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.Azure.Cosmos;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Scribes' names as stored, against the Cosmos DB emulator: the document keeps the shape it had before ids had a type of their own (#128).</summary>
public sealed class ScribeNameRepositoryTests : IAsyncLifetime
{
    private static readonly ScribeId Cache = new("1a2b3c4d-0000-0000-0000-00000000cafe");

    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task NamesSavedBefore_ReadBack_AndSaveInTheSameShape()
    {
        await cosmos.Container.CreateItemAsync(new JsonObject
        {
            ["id"] = ScribeNamesDocument.ScribeNamesId,
            ["partition"] = CosmosCalls.SharedPartition,
            ["type"] = "scribe-names",
            ["names"] = new JsonArray(new JsonObject { ["accessId"] = Cache.Value, ["name"] = "Sister Cache" }),
        }, CosmosCalls.SharedPartitionKey);
        var repository = new ScribeNameRepository(cosmos.Container);

        var stored = Ok(await repository.GetAll(CancellationToken.None));
        Ok(await repository.SaveUnlessChangedSince(stored, [new ScribeName(Cache, "Mother Cache")],
            AuditEntries.Of(TimeProvider.System, Cache, AuditAction.ChooseName, None, None), CancellationToken.None));

        Assert.Equal([new ScribeName(Cache, "Sister Cache")], stored.Names);
        var saved = (await cosmos.Container.ReadItemAsync<JsonObject>(ScribeNamesDocument.ScribeNamesId, CosmosCalls.SharedPartitionKey)).Resource;
        Assert.Equal("""[{"accessId":"1a2b3c4d-0000-0000-0000-00000000cafe","name":"Mother Cache"}]""", saved["names"]?.ToJsonString());
    }
}
