using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// ASP.NET Core integration: HTTP functions take HttpRequest and return IActionResult.
builder.ConfigureFunctionsWebApplication();

// One client for the app's lifetime, as the Cosmos SDK expects (it keeps connections and caches).
var cosmos = builder.Configuration.GetSection(CosmosOptions.SectionName).Get<CosmosOptions>() ?? new CosmosOptions();
builder.Services.AddSingleton(_ => CosmosClients.Create(cosmos));
builder.Services.AddSingleton(services =>
    services.GetRequiredService<CosmosClient>().GetContainer(cosmos.Database, ItemRepository.ContainerName));
builder.Services.AddSingleton<IItemRepository, ItemRepository>();

builder.Build().Run();
