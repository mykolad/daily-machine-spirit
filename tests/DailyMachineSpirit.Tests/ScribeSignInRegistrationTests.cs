using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyMachineSpirit.Tests;

public class ScribeSignInRegistrationTests
{
    [Theory]
    [InlineData("scribes.cloudflareaccess.com", "aud", false, typeof(CloudflareAccessSignIn))]
    [InlineData("scribes.cloudflareaccess.com", "aud", true, typeof(CloudflareAccessSignIn))]
    [InlineData("", "", true, typeof(NoSignIn))]
    [InlineData("", "", false, typeof(NobodySignsIn))]
    // Half set up is not set up: nobody gets in.
    [InlineData("scribes.cloudflareaccess.com", "", false, typeof(NobodySignsIn))]
    public void TheSignIn_FollowsTheSettings_AndFailsClosed(string teamDomain, string audience, bool withoutSignIn, Type expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CloudflareAccess:TeamDomain"] = teamDomain,
            ["CloudflareAccess:Audience"] = audience,
            ["Scriptorium:WithoutSignIn"] = withoutSignIn ? "true" : "false",
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddMetrics()
            .AddSingleton<SiteMetrics>();

        var signIn = services.AddScribeSignIn(configuration).BuildServiceProvider().GetRequiredService<IScribeSignIn>();

        Assert.IsType(expected, signIn);
    }
}
