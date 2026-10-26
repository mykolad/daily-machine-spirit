using DailyMachineSpirit.Mcp;

var builder = WebApplication.CreateBuilder(args);
// The Functions host runs this as its custom handler (host.json) and forwards every request to this port.
var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT")
    ?? throw new InvalidOperationException("FUNCTIONS_CUSTOMHANDLER_PORT isn't set: start it with func start, which sets it.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
// That address overrides the image's HTTP_PORTS, as it should, which ASP.NET Core warns about at every start; errors
// still show.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Error);
builder.Services.AddConnector(builder.Configuration);
builder.Services.AddConnectorTelemetry(builder.Configuration);

var app = builder.Build();
app.UseConnectorTelemetry();
app.MapConnector();
await app.RunAsync();
