using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Telemetry;
using Microsoft.AspNetCore.Http;
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
    [InlineData("", "", true, typeof(EveryoneIsScribeSignIn))]
    [InlineData("", "", false, typeof(NobodyIsScribeSignIn))]
    // Half set up is not set up: nobody gets in.
    [InlineData("scribes.cloudflareaccess.com", "", false, typeof(NobodyIsScribeSignIn))]
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

    // The Expurgators' Access application is another audience; set up without it, nobody may expurgate. Staging's one
    // shared Scribe is an Expurgator too.
    [Theory]
    [InlineData("scribes.cloudflareaccess.com", "aud", "expurgators-aud", false, typeof(CloudflareAccessSignIn))]
    [InlineData("scribes.cloudflareaccess.com", "aud", "", false, typeof(NobodyIsScribeSignIn))]
    [InlineData("", "", "", true, typeof(EveryoneIsScribeSignIn))]
    [InlineData("", "", "", false, typeof(NobodyIsScribeSignIn))]
    public void TheExpurgatorsSignIn_FollowsTheSettings_AndFailsClosed(string teamDomain, string audience, string expurgators, bool withoutSignIn, Type expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CloudflareAccess:TeamDomain"] = teamDomain,
            ["CloudflareAccess:Audience"] = audience,
            ["CloudflareAccess:ExpurgatorAudience"] = expurgators,
            ["Scriptorium:WithoutSignIn"] = withoutSignIn ? "true" : "false",
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddMetrics()
            .AddSingleton<SiteMetrics>();

        var signIn = services.AddScribeSignIn(configuration).BuildServiceProvider().GetRequiredService<ExpurgatorSignIn>();

        Assert.IsType(expected, signIn.SignIn);
    }

    // Access gives /scriptorium/expurgate* its own application, so its tokens name that application: the Expurgators'
    // pages take those alone, and the Scribes' pages never do.
    [Fact]
    public async Task TheExpurgatorsSignIn_TakesTheirApplicationsTokens_AndOnlyThose()
    {
        const string expurgators = "the-expurgators-aud-tag";
        var team = new FakeAccessTeam();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CloudflareAccess:TeamDomain"] = FakeAccessTeam.Domain,
            ["CloudflareAccess:Audience"] = FakeAccessTeam.Audience,
            ["CloudflareAccess:ExpurgatorAudience"] = expurgators,
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddMetrics()
            .AddSingleton<SiteMetrics>()
            .AddScribeSignIn(configuration)
            .AddSingleton<IHttpClientFactory>(team)
            .BuildServiceProvider();
        var expurgatorsToken = team.Token($"https://{FakeAccessTeam.Domain}", expurgators, DateTime.UtcNow.AddHours(1));

        var expurgator = await services.GetRequiredService<ExpurgatorSignIn>().SignIn.Scribe(Request(expurgatorsToken), CancellationToken.None);
        var scribeAsExpurgator = await services.GetRequiredService<ExpurgatorSignIn>().SignIn.Scribe(Request(team.ValidToken()), CancellationToken.None);
        var expurgatorAsScribe = await services.GetRequiredService<IScribeSignIn>().Scribe(Request(expurgatorsToken), CancellationToken.None);

        Assert.Equal(LanguageExt.Prelude.Some(FakeAccessTeam.ScribeId), expurgator);
        Assert.True(scribeAsExpurgator.IsNone);
        Assert.True(expurgatorAsScribe.IsNone);
    }

    private static HttpRequest Request(string token)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[CloudflareAccessSignIn.TokenHeader] = token;
        return request;
    }
}
