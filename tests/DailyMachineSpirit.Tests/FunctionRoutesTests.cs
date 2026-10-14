using System.Globalization;
using System.Reflection;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Tests;

/// <summary>The HTTP routes as the Functions host matches them: in the order of their functions' names, first match wins.</summary>
public sealed class FunctionRoutesTests
{
    // A route whose function's name sorted after the catch-all's would never be reached: the catch-all takes it first.
    [Fact]
    public void TheCatchAll_SortsAfterEveryOtherHttpFunction()
    {
        var names = HttpFunctions().Select(function => function.Name).ToList();
        var catchAll = Assert.Single(HttpFunctions(), function => function.Route.StartsWith("{*", StringComparison.Ordinal));

        Assert.Equal(SitePages.CatchAllName, catchAll.Name);
        Assert.Equal(catchAll.Name, names.Order(StringComparer.Ordinal).Last());
        Assert.Equal(catchAll.Name, names.Order(StringComparer.InvariantCultureIgnoreCase).Last());
        Assert.Equal(catchAll.Name, names.Order(StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.None)).Last());
    }

    private static List<(string Name, string Route)> HttpFunctions()
        => typeof(SitePages).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => (
                Function: method.GetCustomAttribute<FunctionAttribute>(),
                Trigger: method.GetParameters().Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>()).OfType<HttpTriggerAttribute>().FirstOrDefault()))
            .Where(found => found.Function is not null && found.Trigger is not null)
            .Select(found => (found.Function!.Name, found.Trigger!.Route ?? string.Empty))
            .ToList();
}
