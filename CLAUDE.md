# The Daily Machine Spirit — Claude Code Guide

## Project

A satirical website: AI and vibe coding turn software engineers into tech-priests who recite rituals and prompts
instead of understanding their tools. Every day at 00:00 UTC it publishes one **prayer** or **ritual** to the Machine
Spirit, written ahead by an LLM and chosen by moderators, plus a **Heretical Truth**: one or two plain sentences on
what really happens. Either kind is a **rite** (the design's word, and `Rite` in the code). Readers vote with
**Blessed** or **Heresy**.

- Tagline: *In the grim darkness of the far future, no one reads the code.* Motto: *Knowledge is lost. The rituals
  remain.* (`Layout.Motto`): under the rite on Today, where it's read, between brass rules (`.epigraph`), and in the
  footer on every other page; Today's footer leaves it out, so it's said once.
- Everything it runs on in Azure is its own, in one resource group defined in Bicep (`infra/`): the models, a Cosmos DB
  account, a Key Vault and a Functions app per environment. Only the Grafana Cloud stack is shared, with its own token.
- Runs as **one Azure Functions app** (Flex Consumption, .NET 10 isolated worker): a timer generates the daily rite,
  HTTP functions serve the pages and the API. Cloudflare sits in front (`dailymachinespirit.fyi`).

## Solution layout

```
src/DailyMachineSpirit.Functions  — the Functions app (HTTP, timer and queue functions)
  Generation/                     — the backlog and the daily rite: Writing/, Chat/ (models), Judging/ (Jev), Precepts/
  Pages/                          — the public pages, rendered on the server (Today, the archive, a rite's page)
  Tags/                           — the tags' vocabulary, and tagging the rites judged before tags existed
src/DailyMachineSpirit.Data       — Cosmos DB: entities, documents, repositories
tests/DailyMachineSpirit.Tests    — xUnit tests (the repository tests run against the Cosmos DB emulator)
tests/DailyMachineSpirit.SmokeTests — xUnit tests of a deployed app over HTTP (each deployment runs them)
tests/load                        — k6 load tests of the public pages, run by hand against staging (its README)
tools/coverage.ps1                — tests + coverage report + the coverage gate (Build and Test runs it)
tools/grafana-alerts.ps1          — puts the alert rules into Grafana Cloud (observability/README.md)
infra/                            — Bicep for all of Azure (main.bicep); infra/README.md covers what Bicep can't do
observability/                    — the Grafana dashboard and alerts, and what the app's metrics measure
```

## Code style rules

- **No underscore prefix** on private fields (`context`, not `_context`).
- **`this.` only when required** to disambiguate a field from a same-named parameter.
- **No `Async` suffix** on our own methods: the `Task` return type already says it, and there are no synchronous twins
  to tell apart. Framework methods keep their names (`SaveChangesAsync`).
- **Braces always**, even around a one-line body of `if`, `else`, `for`, `foreach` or `while`. The build enforces it
  (IDE0011 in `.editorconfig`); `dotnet format style --diagnostics IDE0011 --severity info` adds missing ones.
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
  folder (`Generation/Judging/`), and the namespace follows the folder.
- **Swappable services sit behind an interface** (`IChatClients`, `IRiteJudge`): the rest of the code never names the
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
  - A draft and the rite it becomes hold the same `RiteWriting`: what the model wrote, and which model, when. The
    documents stay flat; only their mappers know about it.
  - Timestamps are written as UTC ("Z") and read back as UTC.
- **Entra ID only** for Cosmos DB: the app's managed identity in Azure, your `az login` locally
  (`CosmosClients.Create`); the account's keys stay off. The account, databases, container, role assignments and the
  app's settings are all created by the Bicep in `infra/`, never by the app (its data-plane role can't create them). Settings: `Cosmos:Endpoint`, `Cosmos:Database`.
- **Telemetry: OpenTelemetry to Grafana Cloud.** The app (`Program.cs`) exports its logs, traces and metrics over OTLP
  wherever `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `infra/` sets it, with the token's header from the vault. The Functions
  host doesn't export (no `telemetryMode` in `host.json`): its request spans carry visitors' user agents. So a failure
  the host alone would see must be logged by the code. Each environment is its own service (the app's name), tagged with
  `deployment.environment.name`. There's no Application Insights, so the portal's log stream stays empty: look in Grafana.
  The site's own metrics (`SiteMetrics`, `CosmosMetricsHandler`) are listed in `observability/README.md`, next to the
  dashboard; their tags never describe a visitor.
- **The pages** (`Pages/`) follow the design handoff (`design_handoff_daily_machine_spirit`, outside the repo; its
  README is the spec). They're rendered on the server as plain HTML with the CSS and a small script inlined, so a page
  is one request (plus the fonts, cached for a year) and works without the script (the truth then simply shows).
  - The fonts are the site's own copies (`Pages/Fonts/`, Latin subsets, OFL), served at `/fonts/<name>.woff2`: a page
    never makes the visitor's browser contact another site (Google Fonts would learn every visitor's address).
  - The tab's icon is the logo's cog, inline in each page (the Scriptorium's too) as an SVG data URI (`Svg.Favicon`):
    no request for it, and browsers don't ask for `/favicon.ico`.
  - Routes: `/` (Today: the newest rite), `/archive` (`?before=<number>` for older pages, which the archive also
    loads in place as the reader nears the end; `?q=<words>` for search), `/r/<number>`, `/tags/<key>` (a tag's rites,
    paged like the archive). `/search?q=` redirects to `/archive?q=` for good.
    A catch-all route takes `/` and answers every unknown address with the 404 page. The Functions host tries routes in
    the order of their functions' names, first match wins, so the catch-all's name sorts last (`ZzToday`): any other
    route whose function's name sorted after it would never be reached (`FunctionRoutesTests`).
  - Everything a model wrote goes through `Html.Encode` (`Html.WithInlineCode` for text with `backticks`).
  - Every focusable control sits on the dark background, where the amber focus ring shows, except on the Archive's
    parchment sheet, whose rows, field and tag links get a red ring (`docs/design/accessibility.md`).
  - **More rites** (a rite's own page): the three rites whose similarity scores are closest (cosine, `RelatedRites`),
    topped up with the newest others when there aren't enough scored ones. If it fails, the page goes without it.
  - **Recent rites** (Today): the four before today's, read with it in one query, so a new rite's purge of Today
    refreshes them too. The motto sits between the countdown and them.
  - **Wide screens** (1280px and up) give the rite margins: the countdown and tags on the left, the votes on the right,
    nothing the parchment already says. The markup stays in reading order and the stylesheet places it
    (`docs/design/accessibility.md`). The Scriptorium puts its heading on the same altar: candles beside it, the glow
    behind, the traces beyond. The candles and traces are `Styles.Altar`, which the Scriptorium's own stylesheet
    includes.
  - **The Archive** (`ArchivePage`, `ArchiveSheet`, `ArchiveRows`): Today's masthead, then one parchment sheet of rows
    under a rubric per month, 20 to a page, with Today's margins and altar. The left margin counts the rites kept and
    when they began, the right one indexes the months (`RiteRepository.GetDays`, numbers and days only); a month's link
    starts the Archive at that month (`?before=`), so it works without the script and however far back. The script
    loads the next page into the month it belongs to. The sheet wears Today's frame (double rule, corners, code in the
    margins), each row says its number, and the Archive's last page has Today's ribbon and wax seal hanging from the
    sheet (`ArchiveSheet.Closing`). A tag's page is the same sheet, with the other subjects in the right margin.
  - **Cached until they change** (#37): browsers keep a page a minute, and Cloudflare keeps Today and a rite's page an
    hour (`s-maxage`, `EdgeLifetime.UntilPurged`), so their requests rarely reach the app. Whatever changes them purges
    them (`IPageCache`, `CloudflarePageCache`): a saved vote adds its rite's page and Today
    (`Paths.ShowingVotesOf`) to the minute's batch (`PurgeBatcher`), so a vote never waits for Cloudflare and a page is
    purged at most once a minute per instance; a new rite purges Today and the search index at once. The archive and a
    tag's page, which no purge can find, stay a minute, as does any address with a query string (Cloudflare caches each
    apart). Purges go 30 URLs to a request. A purge never fails the change: it's logged and counted (`dms.pages.purges`), and the page
    refreshes within the hour.
    Settings: `Cloudflare:ZoneId`, `:SiteUrl`, `:ApiToken` (a token that may only purge the zone); without them
    (staging, a local run) nothing is purged.
  - **The voter's own counts survive a cached page:** the vote answers with the counts and when they were counted
    (`countedAt`), the page carries when its counts were read (`data-counted-at`, taken before reading), and the script
    keeps each vote's answer for two hours in `localStorage` (`dms-votes-v1`), showing it over an older page.
  - **Tags** (#6): the site's own fixed vocabulary (`Tags/RiteTags`), so there's no `cache` next to `caches`. The
    judge gives a draft its tags with its scores (`IRiteJudge`), and they're stored with the draft and the rite it
    becomes (`tags`, a list of keys), so a tag's page is one query, and switching judges keeps every rite's tags.
    Jev's come from its topic answer (`JevTags`): the topics at 0.25 or more, the likeliest first, at most two, never
    "other", at no extra cost. The rite card, the archive's rows and the Scriptorium's drafts show them; a row's
    title link covers the row, so its tag links can sit inside it.
    - **Rites judged before tags existed** get theirs from their stored scores when the app starts (`TagBackfill`,
      through `IRiteJudge.TagsFrom`); after the first start there's nothing left to tag. Scores the judge can't read
      stay untagged.
    - **Changing how tags are given** (another threshold) applies to new drafts. To retag older ones, remove their
      `tags` and restart the app.
  - **Search** (`/archive?q=`, #7): the Archive filtered to the rites holding every word typed (at most 6, quotes
    ignored) in their title, text or Heretical Truth, ignoring case, newest first, Today's too. Each row marks the words
    (`Highlight`) and shows the words around the first match in the text (`RowLine`); a rite found by its truth alone
    shows its opening words, the truth staying sealed until the reader opens the rite. A plain `CONTAINS` over a few
    hundred rites (each word a query parameter), plus their numbers and days for the count and the month index
    (`FindDays`); Cosmos's full-text index can take over when that grows. The words are never logged, and how many
    rites were found is in the page's title, which a screen reader reads when the results load. Cloudflare rate-limits
    it.
    - **The Archive's field follows the typing:** a quarter of a second after the reader stops, the script fetches
      `/archive?q=` and swaps the sheet, its margins and the address (`history.replaceState`), so live and shared
      results come from the same renderer.
    - **The nav's search** (every page but the Archive, where the icon leads to the page's field): the icon opens a
      combobox, and up to five rites holding every word drop down under it. They're matched in the browser against
      the search index (`/api/search-index`, `SearchIndexFunction`: not the rites, only each one's number, day, kind
      and words as stored, so it matches what the Archive's search matches), loaded once when the field first opens
      and kept at Cloudflare until a new rite purges it (`DailyRiteFunction`; any query string is redirected to the one
      address, so it can't cause a read of its own), so what's typed there never leaves the browser until the reader
      searches. Without the script the icon is a link to the Archive's field.
- **Votes** (Blessed / Heresy, `Api/VotesFunction`): one per account per rite, which the reader can switch or
  take back, signed in as below. `POST /api/rites/{number}/vote` with `{"vote": "blessed" | "heresy" | null}`, and
  `GET /api/votes?rites=12` for the page to show the reader's own vote pressed.
  - **A vote document per account and rite** (`VoteRepository`), its id the rite's number and the voter key, so Cosmos
    itself allows one. The vote and the rite's counts (`blessedVotes`, `heresyVotes`) change in one transactional batch,
    so the counts are always the votes there are.
  - **The anonymous reactions before sign-in** counted in `blessedCount` and `heresyCount`: older rites still have
    them, and nothing reads them, so the counts started again from zero.
  - **The page is the same for everyone:** both "Sign in with GitHub" and "Sign out" are in it, hidden, and the script
    shows the reader's own after asking `/api/me`. The API takes JSON only, so another site's form can't vote.
- **Readers sign in to vote** (`Voters/`, #34), with GitHub for now: `/auth/{provider}?return=<path>` sends them to
  the provider's OAuth web flow, and `/auth/{provider}/callback` brings them back. `/auth/signout` (a form posted from
  this site, `RequestOrigin`) signs them out; `/api/me` tells the page's script whether sign-in is set up and whether
  they're signed in, so the pages stay the same for everyone and cacheable.
  - **A provider is an `ISignInProvider`** (`GitHubSignIn`): its name, whether it's set up, where to send the reader,
    and the account id for the code that comes back. Another provider is another implementation and its settings; the
    routes, cookies and voter keys stay as they are.
  - **Nothing about the account is kept:** the callback exchanges the provider's code for a token, reads only the
    account's id, and revokes the token. The reader becomes a **voter key**, an HMAC of `<provider>:<id>`
    (`github:583231`, `Voters:KeySecret`): the same account always has the same key, and the key doesn't lead back to
    it. An account at another provider is another voter, even if it's the same person.
  - **The cookie** (`dms-voter`, HttpOnly, Secure, SameSite=Lax, 30 days) holds the key, its expiry and a signature
    (`Voters:SessionSecret`). A state cookie ties the provider's answer to the browser and the provider that started,
    and the return path must be a path on this site.
  - **Separate from the Scriptorium:** signing in here makes nobody a Scribe.
  - **Settings:** `Voters:KeySecret` and `:SessionSecret` (32 characters at least, or they count as unset), and each
    provider's own (`GitHub:ClientId`, `:ClientSecret`, `:CallbackUrl`). A provider not set up has no sign-in pages
    (404), and without the voters' secrets nobody signs in. Sign-ins are counted (`dms.signins`, by provider and
    outcome).
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
    request should wait. `host.json` takes one message at a time and tries a message 3 times, 10 minutes apart. The
    queue is declared in `infra/`. Instances can refill at once, but each counts the waiting drafts before every draft
    it writes, so the backlog overshoots by at most one draft per refill running.
  - **A new environment starts with an empty backlog,** so its first midnight writes a rite on the spot, unmoderated.
    To have moderated rites from the first day (production), summon a refill before the first midnight.
  - **Daily publishing can be held** (`Generation:PublishDaily`, `publishesDaily` in `resources.bicep`): off, the timer
    neither publishes nor refills, while the Scriptorium still summons drafts. Both environments publish (production since
    launch, #35).
  - **Writing:** `RiteWriter` asks `gpt-6-sol` for a `RiteContent` (JSON schema output), up to 3 times, then
    `gpt-6-luna` (`Generation:Models`). An answer that's empty, too long, uses a forbidden name or writes code outside
    backticks (`UnquotedCode`) is asked for again, never cut (`RiteChecks`, which a Scribe's amendment passes too).
    Every draft and rewrite follows the Precepts (below); if they can't be read, the rite is written without them.
  - **Judging:** an `IRiteJudge` judges each draft (`JevJudge` for now, swappable like the models): its similarity
    scores for "More rites" and its quality for the Augury, in one request. A draft that can't be judged still joins
    the backlog, without scores. "Scores" are always the similarity scores, never the quality.
  - **Settings:** `AzureOpenAI:Endpoint` (the models are reached as the managed identity), `Jev:ApiKey` (a Key Vault
    reference in Azure; empty turns Jev off), and optionally `Generation:*` (`GenerationOptions`).
- **The Scriptorium** (`/scriptorium`, `Scriptorium/`): where the **Scribes** (moderators) shape the Liturgical Calendar.
  - **Actions:** **Anoint** (next to be published), **Exalt** / **Humble** (up or down one), **Consign to the Flames**
    (burn: the draft is hidden, never deleted), **Restore from the Ashes**, **Summon New Rites** (a refill), **Let
    the Augury Decide** (forget the Scribes' order), and **Amend** (#8, `Amendments`, its own page): change a waiting
    draft's words by hand, or ask the writer to rewrite them as the Scribe's note says (`RiteWriter.Rewrite`; the
    rewrite fills the form, unsaved, for the Scribe to read first). Saved words pass what a model's answer must
    (`RiteChecks`) and are judged again (scores, quality and tags describe what will be published; if the judge fails
    or is off, the draft waits unjudged). The draft keeps `GeneratedByModel` and gets `Edited`, carried into the rite;
    the rite never says which Scribe (the record does). The save is guarded by the draft's ETag, read with the page,
    and records an `Amend` decision with the words and places as they were. The amending page posts plain forms,
    without the calendar's script.
  - **The Scribes' order** (the `placements` document) comes first; the Augury orders the rest. Moving a draft places
    everything above it too, so the order stays what the Scribes saw.
  - **Every action is a lesson:** it saves a `ScribeDecision` with the draft as it was, the Augury's place and quality
    for it, and the Scribe's optional note, in the same batch as the change. The Precepts are learned from them.
  - **The Precepts** (`/scriptorium/precepts`, #12): rules for writing and choosing rites, learned from the Scribes'
    judgments, which the writer follows. One `precepts` document, each change recorded (#9). Its forms carry the
    version the page showed (its ETag), so a change from a page shown before another is refused.
    - **Distilled** (`PreceptDistiller`): `gpt-6-sol` (then `gpt-6-luna`, one try of 30 seconds each) reads the
      newest 100 judgments with their notes and answers at most 12 precepts (JSON schema output). An answer with a
      null, an overlong precept, or nothing but pinned and deleted ones goes to the next model. The `DistilPrecepts`
      timer runs Mondays at 01:00 UTC, as the Machine Spirit, unless no rite was judged since the last distillation
      (the same judgments would only reword the unpinned precepts); a Scribe can ask sooner, and waits for the answer.
      Nothing judged yet, or every precept pinned, means nothing to do.
    - **The Scribes have the final say** (`PreceptKeeping`): they add, edit, pin or delete precepts. Pinned precepts
      stay when distilled again, the rest make way for the new ones; a Scribe's words are pinned. Pinned ones come first,
      on the page and in the writer's prompt; Pin and Unpin say what they do in their `title`. Deleted ones are
      remembered (the newest 30): the model is told not to propose them, and any it does are dropped.
    - **Not yet the judge's:** the Augury still orders by Jev's quality; #10 tests a model judge with the Precepts.
  - **The Record** (`/scriptorium/record`, #9): who changed what, newest first, the last 100; `?draft=` gives one
    draft's whole history. An `AuditEntry` (documents of type `audit`) is saved in the same batch as each change, so
    there's no change without its entry: the Scribes' actions, and the Machine Spirit's own (each draft it writes, each
    rite it publishes). Only Summon, which stores nothing itself, saves its entry alone, and goes ahead if that fails.
    - **Who:** a Scribe is their Access id (the token's `sub`), never their email; on staging, with no sign-in, every
      Scribe is `staging`; the Machine Spirit is `machine-spirit`. The page shows a Scribe by the last six
      characters of their id (`ScribeNames`), until the Scribes choose their names.
    - **Kept** as long as the drafts and rites, with ids, titles and where a draft went; never a visitor.
  - **Concurrency:** each change uses an ETag, so two Scribes can't overwrite each other: the later one is told to look
    again.
  - **Refills:** burning the last waiting draft asks for a refill at once (the `backlog-refills` queue), as does
    Summon.
  - **The page:** plain HTML forms, which work without a script. Each action posts and redirects back to the rite it
    acted on (`#rite-{id}`, marked `acted`, which glows), with a message that names it and where it went; after a burn,
    the message links to the rite in the Ashes, to restore it. It's never cached or indexed, and forms posted from
    another site are refused (`Sec-Fetch-Site`). The icons are inline and decorative: Lucide's
    (`Pages/Lucide-LICENSE.txt`), except Summon's sparkle, which is the design's own. Each action's button says what it
    does in its `title`, the browser's own tooltip on hover: a reminder for the Scribes, chosen over a custom tooltip
    for being simple (the labels themselves stay the rite's names). Each draft's tags show as chips under its title,
    to match against the calendar's subjects.
  - **Shown in place** (`ScriptoriumScript`): where scripts run, an action is posted in the background, the new page's
    calendar replaces the old, and each rite that moved slides from its old place to its new one, so a Scribe sees
    what changed (an Exalt can reorder the rites the Augury places, too). The status message stays the same element,
    so it's announced; focus returns to the same button (after a burn, to the rite now in its place in the calendar,
    which the Augury may have reordered, without scrolling away). Anything unexpected (an error, Cloudflare Access wanting a new sign-in) loads `/scriptorium`
    afresh. Reduced motion: no slide, only the mark.
  - **Access:** `Scriptorium:Enabled` is off by default. Who counts as a Scribe is an `IScribeSignIn`
    (`Scriptorium/SignIn/`), chosen to fail closed (`AddScribeSignIn`):
    - **Cloudflare Access** (`CloudflareAccess:TeamDomain` and `:Audience` set, production): every request must carry
      Access's signed token (`Cf-Access-Jwt-Assertion`), checked against the team's keys, issuer and the application's
      audience, so going around Cloudflare to the app's own address gets nowhere. Its `sub` names the Scribe for the
      record; a token without one is refused. The keys are kept for an hour and
      fetched again when a token names a new one. Refusals are counted (`dms.scriptorium.refusals`, by reason), not
      logged: anyone can send a token to the public address. A failed key fetch is logged once, and the next waits
      five minutes.
    - **No sign-in** (`Scriptorium:WithoutSignIn`): staging, whose IP rule already admits its owner alone, and a local
      run (`local.settings.example.json` sets it).
    - **Neither:** nobody gets in, even with the Scriptorium on.

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

`.github/workflows/build-and-test.yml` (**Build and Test**, job `build-and-test`, the required check) runs on every PR,
and inside every deployment: restore, Release build, a Bicep lint and build of `infra/` (any warning fails it), then
`tools/coverage.ps1 -NoBuild` (the same gate as a local run), with the Cosmos DB emulator as a service container for the
repository tests. For a deployment it also packages the app (the `app` artifact, kept 90 days).

## Deploying

- **Deploy Master** (`deploy-master.yml`): every push to `master` is built and tested again (it calls Build and Test),
  and that package is deployed to staging, then, only if staging passed its smoke tests, to production. No approval:
  staging is the gate, and only `master` may deploy to the `production` environment.
- **Deploy Branch to Staging** (`deploy-branch-to-staging.yml`): run by hand with a branch name, to try a PR on staging.
- Both call `deploy.yml`, which deploys the package as the GitHub environment's identity (OIDC: the environment's
  `AZURE_*` variables hold ids, not secrets), one deployment per environment at a time. Then:
  - **Smoke tests** (`tests/DailyMachineSpirit.SmokeTests`, published into the package, so a package is always checked
    by its own): `/healthz` reports the new commit, and Today, the archive, a rite, a tag's page, a search (and the old address), the votes
    API, the 404 page and a font answer as they should. They read the app's address and expected version from
    `SMOKE_TEST_BASE_URL` and `SMOKE_TEST_VERSION`, so they can also run from your machine against staging. They call
    the app's own address (production's past Cloudflare's cache), and both apps admit only some addresses (staging its
    owner, production Cloudflare), so the workflow adds the runner's address to the access rules for the tests
    (`smoke-tests`) and removes it afterwards, even when the run fails or is cancelled.
  - **A test voter, on staging only:** the smoke tests also sign in as a test voter (`TestVoter`), vote Blessed on the
    newest rite, load the vote as the page does, and take it back, leaving the counts as they were. They sign the
    voter's cookie themselves, with the session secret the workflow reads from staging's vault
    (`test-voter-vault`; staging's GitHub identity may read that one secret, `infra/`). Production gets no secret, so
    the test is skipped there: its counts are public. A staging rollback reads the secret too, for the last good
    package's own vote test.
  - **Rollback:** when the smoke tests fail, the package of the newest successful Deploy Master run goes back and is
    smoke-tested, and the run fails. It's a job of its own beside the deploy (`rollback.yml`, called as `roll back
    staging` or `roll back production`, skipped when nothing failed), so the run's graph shows a rollback as where the
    run ends: production follows staging's deploy alone. Both jobs take the environment's lock (their `concurrency`),
    which is free for a moment between them.
