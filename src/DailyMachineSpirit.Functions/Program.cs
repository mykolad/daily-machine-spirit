using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Chat;
using DailyMachineSpirit.Functions.Generation.Scoring;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = FunctionsApplication.CreateBuilder(args);

// ASP.NET Core integration: HTTP functions take HttpRequest and return IActionResult.
builder.ConfigureFunctionsWebApplication();

// Logs and traces in OpenTelemetry form: the code's logs, each invocation, and its outgoing calls (the models, Jev,
// Cosmos DB). Only the app exports, not the Functions host: the host's request spans carry each visitor's user agent,
// which the site never keeps. Exported only where OTEL_EXPORTER_OTLP_* is set (Grafana Cloud, from infra/); a local
// run exports nothing. The Azure SDKs (Cosmos DB among them) only emit their spans with this switch on.
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
var telemetry = builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddSource("Azure.Cosmos.Operation"))
    .WithLogging();
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    telemetry.UseOtlpExporter();

builder.Services.AddSingleton(TimeProvider.System);

// One client for the app's lifetime, as the Cosmos SDK expects (it keeps connections and caches).
var cosmos = builder.Configuration.GetSection(CosmosOptions.SectionName).Get<CosmosOptions>() ?? new CosmosOptions();
builder.Services.AddSingleton(_ => CosmosClients.Create(cosmos));
builder.Services.AddSingleton(services =>
    services.GetRequiredService<CosmosClient>().GetContainer(cosmos.Database, RiteRepository.ContainerName));
builder.Services.AddSingleton<IRiteRepository, RiteRepository>();
builder.Services.AddSingleton<IDraftRepository, DraftRepository>();
builder.Services.AddSingleton<IScriptoriumRepository, ScriptoriumRepository>();

// The daily rite and the backlog. Created only when the timer or a refill runs, so HTTP functions start without the generation settings.
builder.Services.Configure<GenerationOptions>(builder.Configuration.GetSection(GenerationOptions.SectionName));
builder.Services.Configure<JevOptions>(builder.Configuration.GetSection(JevOptions.SectionName));
var openAI = builder.Configuration.GetSection(AzureOpenAIOptions.SectionName).Get<AzureOpenAIOptions>() ?? new AzureOpenAIOptions();
builder.Services.AddSingleton<IChatClients>(_ => new AzureOpenAIChatClients(new Uri(openAI.Endpoint)));
builder.Services.AddHttpClient<IRiteScorer, JevScorer>(http => http.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddTransient<RiteWriter>();
builder.Services.AddTransient<BacklogRefiller>();
builder.Services.AddTransient<DailyRitePublisher>();

// The Scriptorium: the Scribes' page.
builder.Services.Configure<ScriptoriumOptions>(builder.Configuration.GetSection(ScriptoriumOptions.SectionName));
builder.Services.AddTransient<Scribes>();

builder.Build().Run();
