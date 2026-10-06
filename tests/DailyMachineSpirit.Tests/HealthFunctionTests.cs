using System.Text.Json;
using DailyMachineSpirit.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DailyMachineSpirit.Tests;

public class HealthFunctionTests
{
    [Fact]
    public void Run_ReportsHealthyAndTheVersion()
    {
        var result = new HealthFunction().Run(new DefaultHttpContext().Request);

        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value, JsonSerializerOptions.Web);
        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal(AppVersion.Short, json.GetProperty("version").GetString());
    }

    [Theory]
    [InlineData("1.0.0+3f9c2e1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "3f9c2e1")]
    [InlineData("1.0.0+abc", "abc")]
    [InlineData("1.0.0+", AppVersion.Local)]
    [InlineData("1.0.0", AppVersion.Local)]
    [InlineData(null, AppVersion.Local)]
    public void FromInformationalVersion_TakesTheShortCommit(string? informationalVersion, string expected)
        => Assert.Equal(expected, AppVersion.FromInformationalVersion(informationalVersion));
}
