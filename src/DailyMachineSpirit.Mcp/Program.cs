using DailyMachineSpirit.Mcp;

var builder = WebApplication.CreateBuilder(args);
// The Functions host runs this as its custom handler (host.json) and forwards every request to this port.
var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT")
    ?? throw new InvalidOperationException("FUNCTIONS_CUSTOMHANDLER_PORT isn't set: start it with func start, which sets it.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.AddConnector(builder.Configuration);

var app = builder.Build();
app.MapConnector();
await app.RunAsync();
