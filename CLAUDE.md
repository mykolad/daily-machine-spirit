# The Daily Machine Spirit — Claude Code Guide

## Project

A satirical website: AI and vibe coding turn software engineers into tech-priests who recite rituals and prompts
instead of understanding their tools. Every day at 00:00 UTC it publishes one **prayer** or **ritual** to the Machine
Spirit, written ahead by an LLM and chosen by moderators, plus a **Heretical Truth**: one or two plain sentences on
what really happens. Either kind is a **rite** (the design's word, and `Rite` in the code). Visitors react with
**Blessed** or **Heresy**.

- Tagline: *In the grim darkness of the far future, no one reads the code.* Footer: *Knowledge is lost. The rituals
  remain.*
- Everything it runs on in Azure is its own, in one resource group defined in Bicep (`infra/`): the models, a Cosmos DB
  account, a Key Vault and a Functions app per environment. Only the Grafana Cloud stack is shared, with its own token.
- Runs as **one Azure Functions app** (Flex Consumption, .NET 10 isolated worker): a timer generates the daily rite,
  HTTP functions serve the pages and the API. Cloudflare sits in front (`dailymachinespirit.fyi`).

## Solution layout

```
  Generation/                     — the backlog and the daily rite: Writing/, Chat/ (the models), Scoring/ (Jev)
  Generation/                     — the backlog and the daily rite; Writing/ (prompt, writer), Chat/ (the models), Scoring/ (Jev)
src/DailyMachineSpirit.Data       — Cosmos DB: entities, documents, repositories
tests/DailyMachineSpirit.Tests    — xUnit tests (the repository tests run against the Cosmos DB emulator)
tools/coverage.ps1                — tests + coverage report + the coverage gate (Build and Test runs it)
infra/                            — Bicep for all of Azure (main.bicep); infra/README.md covers what Bicep can't do
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
- **No nulls in our code.** A value that may be missing is `Option<T>` (LanguageExt: `Some`, `None`, `Optional(x)`,
  `Map`, `Bind`, `Match`, `IfNone`).
- **Failures are values.** Anything that can fail at run time (Cosmos, HTTP, the models) returns `Either<Error, T>`, so
  callers have to handle it: a named error for an expected outcome (`RiteRepository.DayAlreadyHasRite`), and
  `Error.New(exception)` for everything else, which keeps the exception for the logs. Only cancellation still throws:
  the caller asked for it. Stick to the readable part of LanguageExt (Option, Either, Error); not `Fin`, `Eff`, `Aff`,
  `Try` or `Validation`.
- **Nullable warnings are build errors** (`Directory.Build.props`), and there's no `null!`. Input and output can still
  carry nulls (JSON documents, HTTP requests and responses, configuration, SDK results), so `T?` belongs only there,
  converted to `Option` where the data enters (e.g. `RiteDocument.Similarity`). The JSON serializers set
  `RespectNullableAnnotations`, so a null arriving in a non-nullable property fails at the boundary instead of
  slipping in.
- **One type per file**, so options, an interface and its implementation are separate files. Related files share a
  folder (`Generation/Scoring/`), and the namespace follows the folder.
- **Swappable services sit behind an interface** (`IChatClients`, `IRiteScorer`): the rest of the code never names the
  vendor, and the vendor's settings (`JevOptions`) belong to its implementation.
- **Member order:** constants and fields, constructor, then public members (in a test class: setup and teardown, then
  the tests), and private helpers last.
- **One blank line between properties** (and between methods), never two in a row. Constants or fields that share one
  comment stay together as a group.
- **Records and `with`** for data: properties are `init`-only, and a changed value is a copy
  (`rite with { Number = number }`), not a mutation.

## Ground rules

- **Legal (unofficial fan satire):** nothing commercial (no ads, donations or shop); no Games Workshop logos, artwork,
  lettering lookalikes or quoted text; no lookalike of the Adeptus Mechanicus cog-skull or the Imperial Aquila; no GW
  trademarks in the name, logo or headings (a single prayer may mention "the Omnissiah"). The generation prompt must
  forbid quoting GW text. The footer carries the GW disclaimer.
- **Privacy:** never store or log anything about visitors beyond what a feature needs (no IPs, no user agents).
- **Accessibility:** WCAG 2.2 AA wins over design fidelity; record each such change in the design docs.
- **No secrets:** Azure is reached with managed identities and GitHub's OIDC; credentials that must exist live in Key
  Vault, and their values are set by hand, never in the repo or in Bicep.
- **Infrastructure as code:** every Azure resource and role assignment is in `infra/` (Bicep). Change Azure by changing
  the Bicep, never by hand in the portal, so the files stay the truth.

## Key design decisions

- **Routes at the site root:** `host.json` sets `routePrefix` to `""`, so `/healthz` is `/healthz`, not `/api/healthz`.
- **`/healthz`** returns `{status, version}`; `version` is the short commit, which the .NET SDK puts into the
  assembly's informational version when it builds in a git checkout (`AppVersion`). A build outside git says `dev`.
- **Data: Cosmos DB, one container `rites`** (partition key `/partition`, NoSQL API, System.Text.Json with camelCase names).
  - A rite's document id is its day (`2026-10-07`), so Cosmos itself allows one rite per day:
    `RiteRepository.Add` returns `DayAlreadyHasRite` on the left of its `Either` for a second one.
  - Cosmos has no auto-increment, so a counter document hands out the rite numbers ("NO. 214"). A transactional
    batch saves the rite and takes its number together, retrying if another save took the number first.
  - Every document is in one partition (`"rites"`): a few hundred small rites a year are far below a partition's
    limits, and a batch needs one partition.
  - `Rite.Similarity` holds the scores for "More rites", whatever method produced them (Jev, to begin with); scores
    from different `ScoresGeneratorVersion`s aren't comparable.
  - Timestamps are written as UTC ("Z") and read back as UTC.
- **Entra ID only** for Cosmos DB: the app's managed identity in Azure, your `az login` locally
  (`CosmosClients.Create`); the account's keys stay off. The account, databases, container, role assignments and the
  app's settings are all created by the Bicep in `infra/`, never by the app (its data-plane role can't create them). Settings: `Cosmos:Endpoint`, `Cosmos:Database`.
- **The backlog and the daily rite** (`Generation/`). Rites are written ahead as **drafts** (`Draft`, documents of
  type `draft` next to the rites), so moderators, the Scribes of the Scriptorium, can choose what's published.
  - **Publishing:** the `DailyRite` timer runs at 00:00 UTC and publishes the top of the **Liturgical Calendar**, unless
    the day already has its rite, so a retry or a caught-up run never replaces one visitors have seen.
    `RiteRepository.Publish` takes the number, saves the rite and marks the draft published in one batch, using the
    draft's ETag, so a draft is never published twice. An empty backlog never skips a day: a draft is written on the
    spot.
  - **The Augury** (`LiturgicalCalendar`) orders the waiting drafts:
    - Better drafts go first. The quality comes from Jev's quality question, a first pick that #10 tests blind.
    - A draft much like the week's rites before it waits.
    - Prayers and rituals take turns when their qualities are close.
    - Drafts without a quality (Jev off or failing) go last.
  - **Refilling:** the `RefillBacklog` function keeps `Generation:BacklogSize` (20) drafts waiting. Each draft is of
    the kind there are fewer of, on a subject neither the recent rites nor the waiting drafts have. The timer asks for a
    refill after publishing, by a message on the `backlog-refills` queue: a full refill takes minutes, longer than a
    request should wait. `host.json` takes one message at a time and tries a message 3 times, 10 minutes apart.
  - **Writing:** `RiteWriter` asks `gpt-6-sol` for a `RiteContent` (JSON schema output), up to 3 times, then
    `gpt-6-luna` (`Generation:Models`). An answer that's empty, too long or uses a forbidden name is asked for again,
    never cut.
  - **Scoring:** an `IRiteScorer` scores each draft (`JevScorer` for now, swappable like the models): its similarity
    scores for "More rites" and its quality for the Augury, in one request. A draft that can't be scored still joins the
    backlog, without scores.
  - **Settings:** `AzureOpenAI:Endpoint` (the models are reached as the managed identity), `Jev:ApiKey` (a Key Vault
    reference in Azure; empty turns Jev off), and optionally `Generation:*` (`GenerationOptions`).
- **The Scriptorium** (`/scriptorium`, `Scriptorium/`): where the **Scribes** (moderators) shape the Liturgical Calendar.
  - **Actions:** **Anoint** (next to be published), **Exalt** / **Humble** (up or down one), **Consign to the Flames**
    (burn: the draft is hidden, never deleted), **Restore from the Ashes**, **Summon New Rites** (a refill), and **Let
    the Augury Decide** (forget the Scribes' order).
  - **The Scribes' order** (the `placements` document) comes first; the Augury orders the rest. Moving a draft places
    everything above it too, so the order stays what the Scribes saw.
  - **Every action is a lesson:** it saves a `ScribeDecision` with the draft as it was, the Augury's place and quality
    for it, and the Scribe's optional note, in the same batch as the change. #12 teaches the Augury from them.
  - **Concurrency:** each change uses an ETag, so two Scribes can't overwrite each other: the later one is told to look
    again.
  - **Refills:** burning the last waiting draft asks for a refill at once (the `backlog-refills` queue), as does
    Summon.
  - **The page:** plain HTML forms, no script. Each action posts and redirects back with what happened. It's never
    cached or indexed, and forms posted from another site are refused (`Sec-Fetch-Site`).
  - **Access:** `Scriptorium:Enabled` is off by default. Staging turns it on, since only its owner's address can reach
    it. Production turns it on only behind Cloudflare Access.

## Pull requests

- **Every change to an open PR is a new commit on top.** Don't amend, squash or force-push commits that are already
  pushed: whoever is reviewing or has the branch checked out keeps a stable history. Squashing, if wanted, happens at
  merge.
- **To bring a PR up to date with `master`, merge `master` into its branch** (a merge commit); don't rebase. `master`
  requires branches to be up to date before merging.

## Building and testing

```
dotnet build DailyMachineSpirit.slnx
./tools/coverage.ps1        # tests, the coverage gate, HTML report at coverage/index.html
```

The repository tests need the Cosmos DB emulator (every test gets its own throwaway database, so they never see each
other's data):

```
docker run -d -p 8081:8081 -p 8080:8080 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest --protocol https
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

For anything that reads data, set `Cosmos:Endpoint` and `Cosmos:Database` in `local.settings.json` (e.g. staging's
database, as your `az login`; never production's). HTTP functions run without storage. The timer also needs `AzureOpenAI:Endpoint` (staging's, with the Cognitive Services OpenAI User role for your
`az login`); `Jev:ApiKey` can stay empty. Timer functions need `AzureWebJobsStorage`: run the Azurite emulator
(`docker run -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite`).

## Build and Test

`.github/workflows/build-and-test.yml` (**Build and Test**, job `build-and-test`, the required check) runs on every PR
and on pushes to `master`: restore, Release build, a Bicep lint and build of `infra/` (any warning fails it), then
`tools/coverage.ps1 -NoBuild` (the same gate as a local run), with the Cosmos DB emulator as a service container for the
repository tests.
