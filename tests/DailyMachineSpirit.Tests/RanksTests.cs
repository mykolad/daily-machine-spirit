using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

/// <summary>The offices a reader rises through by the rites they judge (#201).</summary>
public class RanksTests
{
    [Theory]
    [InlineData(0, "Supplicant")]
    [InlineData(1, "Initiate")]
    [InlineData(9, "Initiate")]
    [InlineData(10, "Acolyte")]
    [InlineData(25, "Lector")]
    [InlineData(50, "Keeper of Rites")]
    [InlineData(100, "Archivist")]
    [InlineData(249, "Archivist")]
    [InlineData(250, "Elder of the Shrine")]
    [InlineData(1000, "Elder of the Shrine")]
    public void TheOffice_IsTheHighestTheCountHasReached(int judged, string office)
        => Assert.Equal(office, Ranks.Of(judged).Name);

    [Theory]
    [InlineData(0, "1 more rite to Initiate", 0)]
    [InlineData(16, "9 more rites to Lector", 40)]
    [InlineData(24, "1 more rite to Lector", 93)]
    [InlineData(300, "The shrine has no higher office", 100)]
    public void TheWayToTheNext_IsSaidAndMeasured(int judged, string said, int progress)
        => Assert.Equal((said, progress), (Ranks.ToNext(judged), Ranks.Progress(judged)));
}
