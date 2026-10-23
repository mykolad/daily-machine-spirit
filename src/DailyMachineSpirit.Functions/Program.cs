using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Chat;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.Expurgation;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Snapshot;
using DailyMachineSpirit.Functions.Tags;
using DailyMachineSpirit.Functions.Telemetry;
using DailyMachineSpirit.Functions.Voters;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = FunctionsApplication.CreateBuilder(args);

// How the Machine Spirit works: the values worth tuning, in one place (machine-spirit.jsonc). The app's own settings come
// after, so one of the same name (Section__Key) would override a value, though none needs to.
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "machine-spirit.jsonc"), optional: false, reloadOnChange: false)
    .AddEnvironmentVariables();
// A key the section doesn't have (a misspelling) fails when the settings are first used, rather than quietly leaving a
// value unset (and SettingsTests read the file the same way).
void Tuned<T>(string section) where T : class
    => builder.Services.Configure<T>(builder.Configuration.GetSection(section), binder => binder.ErrorOnUnknownConfiguration = true);

// ASP.NET Core integration: HTTP functions take HttpRequest and return IActionResult.
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<InvocationMetricsMiddleware>();

// Logs, traces and metrics in OpenTelemetry form: the code's logs, each invocation, and its outgoing calls (the models,
// Jev, Cosmos DB). Only the app exports, not the Functions host: the host's request spans carry each visitor's user
// agent, which the site never keeps. Exported only where OTEL_EXPORTER_OTLP_* is set (Grafana Cloud, from infra/); a
// local run exports nothing. The Azure SDKs (Cosmos DB among them) only emit their spans with this switch on.
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
var telemetry = builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .KeepConfiguredResourceAttributes()
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddSource("Azure.Cosmos.Operation"))
    .WithMetrics(metrics => metrics
        .AddMeter(SiteMetrics.MeterName)
        // .NET's own: memory, garbage collection, the thread pool.
        .AddMeter("System.Runtime")
        .AddHttpClientInstrumentation()
        // Request units, duration and status codes of every Cosmos DB request.
        .AddMeter(CosmosMetricsHandler.MeterName))
    .WithLogging();
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    telemetry.UseOtlpExporter();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMetrics();
builder.Services.AddSingleton<SiteMetrics>();

// One client for the app's lifetime, as the Cosmos SDK expects (it keeps connections and caches).
var cosmos = builder.Configuration.GetSection(CosmosOptions.SectionName).Get<CosmosOptions>() ?? new CosmosOptions();
builder.Services.AddSingleton(_ => CosmosClients.Create(cosmos));
builder.Services.AddSingleton(services =>
    services.GetRequiredService<CosmosClient>().GetContainer(cosmos.Database, RiteRepository.ContainerName));
// The rites read from Cosmos DB, or from their snapshot outside the region while Cosmos DB doesn't answer (#112).
var snapshot = builder.Configuration.GetSection(RiteSnapshotOptions.SectionName).Get<RiteSnapshotOptions>() ?? new RiteSnapshotOptions();
builder.Services.AddSingleton<IRiteSnapshotStore>(_ => snapshot.ContainerUri.Length > 0
    ? new BlobRiteSnapshotStore(new Uri(snapshot.ContainerUri))
    : new NoRiteSnapshot());
// What saves reads Cosmos DB alone; the public pages, search and the rites API read through the fallback.
builder.Services.AddSingleton<RiteRepository>();
builder.Services.AddSingleton<IRiteRepository>(services => services.GetRequiredService<RiteRepository>());
builder.Services.AddSingleton<IRiteReader>(services => new RitesWithFallback(
    services.GetRequiredService<RiteRepository>(), services.GetRequiredService<IRiteSnapshotStore>(),
    services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger<RitesWithFallback>>()));
builder.Services.AddSingleton<RiteSnapshots>();
builder.Services.AddSingleton<IDraftRepository, DraftRepository>();
builder.Services.AddSingleton<IScriptoriumRepository, ScriptoriumRepository>();
builder.Services.AddSingleton<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<IExpurgationRepository, ExpurgationRepository>();
builder.Services.AddSingleton<IPreceptRepository, PreceptRepository>();
builder.Services.AddSingleton<IScribeNameRepository, ScribeNameRepository>();
builder.Services.AddSingleton<IVoteRepository, VoteRepository>();
builder.Services.AddSingleton<IBurnRepository, BurnRepository>();
builder.Services.AddSingleton<IAilmentRepository, AilmentRepository>();

// The daily rite and the backlog. Created only when the timer or a refill runs, so HTTP functions start without the generation settings.
Tuned<RiteWritingOptions>(RiteWritingOptions.SectionName);
Tuned<BacklogOptions>(BacklogOptions.SectionName);
Tuned<DuplicateRiteCheckOptions>(DuplicateRiteCheckOptions.SectionName);
Tuned<AilmentWritingOptions>(AilmentWritingOptions.SectionName);
Tuned<PreceptDistillingOptions>(PreceptDistillingOptions.SectionName);
Tuned<PreceptCheckOptions>(PreceptCheckOptions.SectionName);
Tuned<AuguryOptions>(AuguryOptions.SectionName);
Tuned<JevOptions>(JevOptions.SectionName);
Tuned<QualityJudgingOptions>(QualityJudgingOptions.SectionName);
var jev = builder.Configuration.GetSection(JevOptions.SectionName).Get<JevOptions>() ?? new JevOptions();
var openAI = builder.Configuration.GetSection(AzureOpenAIOptions.SectionName).Get<AzureOpenAIOptions>() ?? new AzureOpenAIOptions();
builder.Services.AddSingleton<IChatClients>(_ => new AzureOpenAIChatClients(openAI.Endpoint));
builder.Services.AddHttpClient<ISimilarityJudge, JevJudge>(http => http.Timeout = jev.Patience.TryLimit.IfNone(Timeout.InfiniteTimeSpan));
builder.Services.AddTransient<IQualityJudge, RubricJudge>();
builder.Services.AddTransient<IRiteJudge, RiteJudge>();
// Tags the rites judged before tags existed, once per start (nothing left after the first).
builder.Services.AddHostedService<TagBackfill>();
// Gives the drafts burned before a draft kept its burn's details the Record's, once per start.
builder.Services.AddHostedService<BurnBackfill>();
builder.Services.AddHostedService<AilmentBackfill>();
builder.Services.AddTransient<RiteWriter>();
builder.Services.AddTransient<DuplicateCheck>();
builder.Services.AddTransient<AilmentWriter>();
builder.Services.AddTransient<DraftJudging>();
builder.Services.AddTransient<BacklogRefiller>();
builder.Services.AddTransient<DailyRitePublisher>();
builder.Services.AddTransient<PreceptCheck>();
builder.Services.AddTransient<PreceptDistiller>();

// The Scriptorium: the Scribes' page.
Tuned<ScriptoriumPagesOptions>(ScriptoriumPagesOptions.SectionName);
Tuned<ScribeNameSuggestionsOptions>(ScribeNameSuggestionsOptions.SectionName);
builder.Services.AddTransient<Scribes>();
builder.Services.AddTransient<TabCounting>();
builder.Services.AddTransient<Expurgations>();
builder.Services.AddTransient<Amendments>();
builder.Services.AddTransient<PreceptKeeping>();
builder.Services.AddTransient<ScribeNaming>();
builder.Services.AddTransient<NameSuggester>();
builder.Services.AddTransient<AgreementTrend>();
builder.Services.AddScribeSignIn(builder.Configuration);

// Readers sign in to vote, with each provider set up (GitHub for now); separate from the Scriptorium's sign-in.
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.Configure<VoterOptions>(builder.Configuration.GetSection(VoterOptions.SectionName));
builder.Services.AddHttpClient<ISignInProvider, GitHubSignIn>(http => http.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<VoterSession>();

// Cloudflare keeps the pages until they change; votes (batched, once a minute) and publishing purge them (production only).
builder.Services.Configure<CloudflareOptions>(builder.Configuration.GetSection(CloudflareOptions.SectionName));
Tuned<PagePurgingOptions>(PagePurgingOptions.SectionName);
Tuned<PublicPagesOptions>(PublicPagesOptions.SectionName);
builder.Services.AddHttpClient(CloudflarePageCache.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IPageCache, CloudflarePageCache>();
builder.Services.AddSingleton<PurgeBatcher>();
builder.Services.AddHostedService(services => services.GetRequiredService<PurgeBatcher>());

builder.Build().Run();
