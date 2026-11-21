using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class RelicBadgeTests
{
    [Theory]
    [InlineData(1, ">Most venerated<")]
    [InlineData(2, ">2nd most venerated<")]
    [InlineData(3, ">3rd most venerated<")]
    [InlineData(4, ">4th most venerated<")]
    [InlineData(11, ">11th most venerated<")]
    [InlineData(12, ">12th most venerated<")]
    [InlineData(13, ">13th most venerated<")]
    [InlineData(21, ">21st most venerated<")]
    public void For_ARiteTheReliquaryKeeps_SaysItsPlace(int place, string said)
        => Assert.Contains(said, RelicBadge.For(7, new Dictionary<int, int> { [7] = place }));

    [Fact]
    public void For_ARiteTheReliquaryDoesntKeep_IsEmpty()
        => Assert.Empty(RelicBadge.For(7, new Dictionary<int, int> { [8] = 1 }));

    [Theory]
    [InlineData(1, ">Most confessed<")]
    [InlineData(3, ">3rd most confessed<")]
    [InlineData(12, ">12th most confessed<")]
    public void ConfessionBadge_ForARiteAmongTheMostConfessed_SaysItsPlace_AndLeadsToThatOrder(int place, string said)
    {
        var badge = ConfessionBadge.For(7, new Dictionary<int, int> { [7] = place });

        Assert.Contains(said, badge);
        Assert.Contains("href=\"/reliquary/confessed\"", badge);
    }
}
