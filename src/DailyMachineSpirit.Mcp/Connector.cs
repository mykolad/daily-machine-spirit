using ModelContextProtocol.AspNetCore;

namespace DailyMachineSpirit.Mcp;

/// <summary>The connector's services and routes, apart from Program.cs so the tests can host them on a test server.</summary>
public static class Connector
{
    public const string McpPath = "/mcp";

    public static IServiceCollection AddConnector(this IServiceCollection services, IConfiguration configuration)
    {
        var api = configuration.GetSection(RitesApiOptions.SectionName).Get<RitesApiOptions>() ?? new RitesApiOptions();
        services.AddHttpClient<RitesApi>(http =>
        {
            http.BaseAddress = new Uri(api.BaseUrl);
            // Within what an assistant waits for a tool; the API answers from Cloudflare's copies in well under a second.
            http.Timeout = TimeSpan.FromSeconds(20);
        });
        // Stateless: every request carries all it needs, so any instance answers it, with no session to keep or route
        // back to. The tools only read, so they lose nothing by it.
        services.AddMcpServer()
            .WithHttpTransport(transport => transport.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<RiteTools>();
        return services;
    }

    public static WebApplication MapConnector(this WebApplication app)
    {
        app.MapMcp(McpPath);
        // What Traffic Manager probes and the deploys' smoke tests wait for, as on the site.
        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", version = ConnectorVersion.Short }));
        return app;
    }
}
