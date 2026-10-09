using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class RelatedRitesTests
{
    [Fact]
    public void Closest_OrdersByLikeness_AndLeavesOutTheRiteItself()
    {
        var scores = new Dictionary<int, float[]>
        {
            [1] = [1f, 0f, 0f],
            [2] = [0.9f, 0.1f, 0f],
            [3] = [0f, 1f, 0f],
            [4] = [0.5f, 0.5f, 0f],
            [5] = [0f, 0f, 1f],
        };

        Assert.Equal([2, 4, 5], RelatedRites.Closest(1, [1f, 0f, 0f], scores));
    }

    [Fact]
    public void Closest_SkipsScoresOfAnotherShape_AndRanksEmptyOnesLast()
    {
        var scores = new Dictionary<int, float[]>
        {
            [2] = [1f, 0f],
            [3] = [0f, 0f, 0f],
            [4] = [0f, 1f, 0f],
        };

        Assert.Equal([4, 3], RelatedRites.Closest(1, [1f, 1f, 0f], scores));
    }

    [Fact]
    public void Closest_OfEquallyLikeRites_PrefersTheNewer()
        => Assert.Equal([9, 7, 3], RelatedRites.Closest(1, [1f], new Dictionary<int, float[]> { [3] = [1f], [9] = [1f], [7] = [1f], [2] = [1f] }));
}
