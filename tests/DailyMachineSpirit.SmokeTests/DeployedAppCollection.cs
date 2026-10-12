namespace DailyMachineSpirit.SmokeTests;

[CollectionDefinition(Name)]
public sealed class DeployedAppCollection : ICollectionFixture<DeployedApp>
{
    public const string Name = "Deployed app";
}
