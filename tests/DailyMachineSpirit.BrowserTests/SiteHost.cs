using System.Globalization;
using System.Reflection;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Telemetry;
using DailyMachineSpirit.Functions.Voters;
using DailyMachineSpirit.Tests;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.BrowserTests;

/// <summary>
/// The site as a reader's browser meets it, served in this process on a local port: the app's own page and API
/// functions, found by their <c>[Function]</c> and routes as the Functions host finds them, over the test's emulator
/// database, with a fake sign-in. Never the app's settings: a local run of the real host reads production's.
/// </summary>
public sealed class SiteHost : IAsyncDisposable
{
    // What a reader's pages call: the pages, voting, witnessed rites, who's signed in, drawing, search, the fonts.
    private static readonly Type[] Served =
        [typeof(SitePages), typeof(VotesFunction), typeof(WitnessedFunction), typeof(SignInFunction), typeof(DrawFunction), typeof(SearchIndexFunction), typeof(FontsFunction)];

    private static readonly VoterOptions Voters = new()
    {
        KeySecret = "browser-tests-key-secret-of-32-characters",
        SessionSecret = "browser-tests-session-secret-of-32-characters",
    };

    private readonly WebApplication app;

    private SiteHost(WebApplication app)
    {
        this.app = app;
    }

    /// <summary>Where the browser finds the site: <c>http://127.0.0.1:port</c>.</summary>
    public string Address => app.Urls.First();

    public static async Task<SiteHost> Start(Container container)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var services = builder.Services;
        services.AddRouting();
        services.AddMvcCore();
        services.AddMetrics();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        services.AddSingleton(container);
        services.AddSingleton<RiteRepository>();
        services.AddSingleton<IRiteReader>(provider => provider.GetRequiredService<RiteRepository>());
        services.AddSingleton<IVoteRepository, VoteRepository>();
        services.AddSingleton<IWitnessedRepository, WitnessedRepository>();
        services.AddSingleton(Options.Create(Settings.PublicPages));
        services.AddSingleton(Options.Create(Settings.PagePurging));
        services.AddSingleton(Options.Create(Voters));
        services.AddSingleton<VoterSession>();
        services.AddSingleton<SiteMetrics>();
        services.AddSingleton<IPageCache, FakePageCache>();
        services.AddSingleton<PurgeBatcher>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<PurgeBatcher>());
        services.AddSingleton<ISignInProvider>(new FakeSignInProvider("github", true, "583231"));

        var app = builder.Build();
        foreach (var (method, trigger) in Served.SelectMany(HttpFunctions))
        {
            var methods = (trigger.Methods ?? ["get"]).Select(name => name.ToUpperInvariant()).ToArray();
            app.MapMethods("/" + (trigger.Route ?? string.Empty), methods, context => Invoke(context, method));
        }
        await app.StartAsync();
        return new SiteHost(app);
    }

    /// <summary>The sign-in cookie of a reader signed in with this account, as the app issues it.</summary>
    public string SessionOf(string accountId)
    {
        var session = app.Services.GetRequiredService<VoterSession>();
        return session.Issue(session.VoterKeyFor("github", accountId));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IEnumerable<(MethodInfo Method, HttpTriggerAttribute Trigger)> HttpFunctions(Type type)
        => type.GetMethods()
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Select(method => (Method: method, Trigger: method.GetParameters()
                .Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>())
                .OfType<HttpTriggerAttribute>()
                .FirstOrDefault()))
            .Where(found => found.Trigger is not null)
            .Select(found => (found.Method, found.Trigger!));

    // As the Functions host calls a function: the request, the route's values by name, the cancellation token.
    private static async Task Invoke(HttpContext context, MethodInfo method)
    {
        var function = ActivatorUtilities.CreateInstance(context.RequestServices, method.DeclaringType!);
        var arguments = method.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(HttpRequest) ? context.Request
            : parameter.ParameterType == typeof(CancellationToken) ? context.RequestAborted
            : Convert.ChangeType(context.GetRouteValue(parameter.Name!), parameter.ParameterType, CultureInfo.InvariantCulture))
            .ToArray();
        var returned = method.Invoke(function, arguments);
        if (returned is Task task)
        {
            await task;
            returned = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
        }
        var result = (IActionResult)returned!;
        await result.ExecuteResultAsync(new ActionContext(context, context.GetRouteData(), new ActionDescriptor()));
    }
}
