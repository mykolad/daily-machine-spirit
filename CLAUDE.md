# The Daily Machine Spirit — Claude Code Guide

## Project

A satirical website: AI and vibe coding turn software engineers into tech-priests who recite rituals and prompts
instead of understanding their tools. Every day at 00:00 UTC an LLM writes one **prayer** or **ritual** to the Machine
Spirit, plus a **Heretical Truth**: one or two plain sentences on what really happens. Visitors react with **Blessed**
or **Heresy**.

- Tagline: *In the grim darkness of the far future, no one reads the code.* Footer: *Knowledge is lost. The rituals
  remain.*
- It runs on existing shared Azure resources (the SQL server and database, Key Vault, the Azure OpenAI models, Grafana)
  rather than its own copies.
- Runs as **one Azure Functions app** (Flex Consumption, .NET 10 isolated worker): a timer generates the daily item,
  HTTP functions serve the pages and the API. Cloudflare sits in front (`dailymachinespirit.fyi`).

## Solution layout

```
src/DailyMachineSpirit.Functions  — the Functions app (HTTP and timer functions)
src/DailyMachineSpirit.Data       — EF Core: DbContext, entities, migrations, repositories
tests/DailyMachineSpirit.Tests    — xUnit unit tests (SQLite in memory for the repositories; SQL Server in CI)
tools/coverage.ps1                — tests + coverage report + the coverage gate (Build and Test runs it)
```

## Code style rules

- **No underscore prefix** on private fields (`context`, not `_context`).
- **`this.` only when required** to disambiguate a field from a same-named parameter.
- **No `Async` suffix** on our own methods: the `Task` return type already says it, and there are no synchronous twins
  to tell apart. Framework methods keep their names (`SaveChangesAsync`).
- **No default arguments** on method parameters: callers always pass explicitly
  (e.g. `CancellationToken cancellationToken`, never `= default`).
- **Tests check behaviour, not logs.** Assert on outcomes: what's saved, returned or sent. Never on log messages, and
  don't add tests whose only purpose is a log line. To wait for background work, wait for its observable result.
- Spell out names in workflows and jobs ("Deploy Master", not "CD").

## Ground rules

- **Legal (unofficial fan satire):** nothing commercial (no ads, donations or shop); no Games Workshop logos, artwork,
  lettering lookalikes or quoted text; no lookalike of the Adeptus Mechanicus cog-skull or the Imperial Aquila; no GW
  trademarks in the name, logo or headings (a single prayer may mention "the Omnissiah"). The generation prompt must
  forbid quoting GW text. The footer carries the GW disclaimer.
- **Privacy:** never store or log anything about visitors beyond what a feature needs (no IPs, no user agents).
- **Accessibility:** WCAG 2.2 AA wins over design fidelity; record each such change in the design docs.
- **No secrets:** Azure is reached with managed identities and GitHub's OIDC; credentials that must exist live in Key
  Vault.

## Key design decisions

- **Routes at the site root:** `host.json` sets `routePrefix` to `""`, so `/healthz` is `/healthz`, not `/api/healthz`.
- **`/healthz`** returns `{status, version}`; `version` is the short commit, which the .NET SDK puts into the
  assembly's informational version when it builds in a git checkout (`AppVersion`). A build outside git says `dev`.
- **Data:** one `Items` row per UTC day (a unique index on `PublishedOnUtc`, so two generations for the same day
  can't both save; `ItemRepository.TryAdd` returns false for the loser). `ItemProfiles` keeps each item's
  similarity scores for "More rites", whatever method produced them (Jev, to begin with); scores from different
  `ScoresGeneratorVersion`s aren't comparable.
- **The production database is shared with another app**, so everything lives in the `machinespirit` schema, including
  the migrations history (`machinespirit.__EFMigrationsHistory`, set in `MachineSpiritDbContext.ConfigureSqlServer`).
  Nothing may touch `dbo`. CI checks this (below).
- **Entra ID only** for SQL: the connection string uses `Authentication=Active Directory Managed Identity` in Azure and
  `Active Directory Default` locally; no password anywhere. The connect timeout is raised to 60 s
  (`SqlConnectionStrings.WithResumeTimeout`), so a paused serverless database (staging) can resume during the login.

## EF Core migrations

The Functions app is the startup project (the tools build its host to get the DbContext); there's no design-time
factory:

```
dotnet ef migrations add <Name> --project src/DailyMachineSpirit.Data --startup-project src/DailyMachineSpirit.Functions
```

The app never migrates on startup; the deploys will run a migration bundle.

## Building and testing

```
dotnet build DailyMachineSpirit.slnx
./tools/coverage.ps1        # tests, the coverage gate, HTML report at coverage/index.html
```

The minimum line coverage is **95%** (`$MinLineCoverage` in `tools/coverage.ps1`). If some code really can't be covered,
ask the owner before lowering it; never lower it just to get a PR through.

## Running locally

Needs Azure Functions Core Tools v4 (`winget install Microsoft.Azure.FunctionsCoreTools`).

```
cd src/DailyMachineSpirit.Functions
cp local.settings.example.json local.settings.json   # once; local.settings.json is git-ignored
func start
```

For anything that reads the database, set `ConnectionStrings:DefaultConnection` in `local.settings.json` (e.g. the
staging database with `Authentication=Active Directory Default`, as your `az login`; never production's). HTTP
functions run without storage. Timer functions need `AzureWebJobsStorage`: run the Azurite emulator
(`docker run -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite`).

## Build and Test

`.github/workflows/build-and-test.yml` (**Build and Test**, job `build-and-test`) runs on every PR and on pushes to
`master`, with two required jobs:
- **`build-and-test`:** restore, Release build, then `tools/coverage.ps1 -NoBuild` (the same gate as a local run).
- **`clean-database-migrations`**, against a throwaway SQL Server 2022 container (SQL auth; its password protects nothing
  but that container):
  1. `dotnet ef migrations has-pending-model-changes`: a model change without a migration fails the PR.
  2. A migration bundle applies the whole chain to a database that already holds another app's tables and migrations
     history in `dbo` (standing in for the shared production database), rolls every migration back (`efbundle 0`),
     and applies them again. Then it checks that our history is in `machinespirit` and `dbo` is untouched.
  3. The repository tests (`*RepositoryTests`) run against SQL Server: with `SQLSERVER_TEST_CONNECTION` set,
     `TestDatabase` gives each test class its own database built by the migrations (SQLite otherwise).
