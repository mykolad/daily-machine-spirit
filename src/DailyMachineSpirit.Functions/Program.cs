using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// ASP.NET Core integration: HTTP functions take HttpRequest and return IActionResult.
builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
