# Observability

The app exports its logs, traces and metrics to Grafana Cloud over OTLP (`Program.cs`; the settings come from `infra/`).
Each environment is its own service, named after its Functions app (`machinespirit-app-staging`).

## The dashboard

`grafana/daily-machine-spirit.json`: traffic, Cosmos DB, the daily rite and the backlog, .NET and logs. To add or update it, in
Grafana: **Dashboards → New → Import**, and upload the file. It keeps its uid, so importing a newer version replaces the
old one. Change it in Grafana if you like, then export it (Share → Export, without "Export for sharing externally") back
over this file, so the repository stays the truth.

- **Its Metrics and Logs pickers offer only the stack's `grafanacloud-*-prom` and `grafanacloud-*-logs`,** as
  `tools/grafana-alerts.ps1` picks them. The stack has other Loki data sources (alert-state history, usage insights)
  that sort first: an unfiltered picker can choose one of them, and the log panels then show no data.
- **A regular expression in a LogQL query goes in backticks** (`` scope_name=~`Generation\..*` ``): in double quotes,
  `\.` is an escape LogQL doesn't know, and the query fails to parse.

## The activity dashboard

`grafana/daily-machine-spirit-activity.json`: what the site's people do, beside the technical dashboard above. Imported
and exported the same way.

- **The Scribes' actions, readers' votes, sign-ins and Archive searches** are counted from the app's logs (`LogCounters`,
  one line each, below), not metrics. Each line says what was done, never who or what was searched for.

  A counter would miss most of them. Each process counts from zero and sends its totals every minute, and
  `increase()` measures how a total grew between samples, so a series' first sample, with nothing before it, adds
  nothing. Processes come and go with traffic: one that serves a single vote sends a single 1, and `increase()` reports
  0. Grafana Cloud doesn't add the missing zero: on staging, one refused sign-in left `dms_signins_total` at 1 and
  `increase()` at 0 (October 2026). Starting a counter at zero only helps if the zero is sent before the first event,
  which usually comes with the very request that started the process. A log line is one event, however short its
  process's life.
- **Page views, visitors and how much Cloudflare's cache served** come from Cloudflare's own analytics, for production
  (staging isn't behind Cloudflare). Nothing new is collected about visitors: Grafana reads the zone's daily totals
  through Cloudflare's GraphQL API. It needs, once:
  1. **A read-only token** in Cloudflare: **My Profile → API Tokens → Create Token → Custom token**, permission
     **Zone · Analytics · Read**, zone resources **Include · Specific zone · dailymachinespirit.fyi**. Nothing else: it
     can't purge, change or read anything but the totals.
  2. **The Infinity data source** in Grafana (**Connections → Add new connection → Infinity**, install it if the stack
     hasn't), named so it has **Cloudflare** in it (the dashboard's picker looks for that). **Authentication: Bearer
     token**, with the token pasted there, where Grafana keeps it encrypted; **Allowed hosts:**
     `https://api.cloudflare.com`. The token stays there: never in the repository or a dashboard.
  3. **The zone's ID** in the dashboard's *Cloudflare zone ID* field (the zone's Overview page shows it). Save the
     dashboard once it's set, so it stays.

| Counted (`LogCounters`, by its attribute) | Values |
|---|---|
| A Scribe's action (`ScribeAction`) | `anoint`, `exalt`, `humble`, `release` (back to the Augury), `burn`, `restore`, `amend`, `rewrite` (asked the Machine Spirit), `judge` (again), `summon`, `augury` (let it decide), `distil`, `precept-add`, `precept-edit`, `precept-pin`, `precept-unpin`, `precept-delete`, `name`, `expurgate` (erased words, #97) |
| A reader's vote (`Vote`) | `blessed`, `heresy`, `withdrawn`; only a vote that changed something, not the same one sent again |
| A reader's sign-in (`SignIn`, with `Provider`) | as `dms_signins_total`: `signed-in`, `declined`, `refused`, `failed` |
| A search of the Archive (`Search`) | `words`, `words-in-a-subject`; the first page of each search the app answers (the same search again within a minute is answered from a cache, and not counted), and a pause while typing in the Archive's field is a search too |

## The alerts

Production only (the service `machinespirit-app`), so staging's experiments never wake anyone. Each one's description says
what to look at.

| Alert | Fires when |
|---|---|
| **No rite today** | Today doesn't show today's rite at 02:00 UTC, after the timer and its retries. A workflow (**Check Today's Rite**), not Grafana: it reads the page readers get, through Cloudflare's cache, and opens an issue. |
| **Refills failing** | a backlog refill failed all three tries within an hour (its error logs) |
| **Backlog ran empty** | publishing found no waiting draft more than once in a week (its warning logs) |
| **Jev failing** | two or more drafts in a day couldn't be judged (the refiller's warning logs) |
| **Server errors** | more than 5% of requests answered 5xx for ten minutes, over at least ten requests |
| **Today unreachable or stale** | more than half of the last 15 minutes' synthetic checks of Today failed, for five minutes: no 200, a `STALE` copy from Cloudflare (the app didn't answer), or yesterday's rite after 01:30 UTC (below) |
| **No telemetry from production** | nothing for 26 hours: the midnight timer wakes the app every day, so silence means a broken export or a dead app |

Rare events are counted from logs, not metrics, for the reason the activity dashboard's are (above): a counter's first
value in a process, often its only one for something that happens once a day, is never seen as an increase.

The Grafana rules are `grafana/alerts.json`, one rule group, evaluated every five minutes, and they notify through the
stack's default notification policy (the default contact point emails the stack's owner). To put them into Grafana:

1. **Administration → Users and access → Service accounts → Add service account** (role **Editor**, enough for the
   folder and the rules on Grafana Cloud in October 2026), then **Add service account token**. Save the token to
   `$HOME\.machinespirit\grafana-alerts-token.txt`.
2. Run `./tools/grafana-alerts.ps1 -GrafanaUrl https://<stack>.grafana.net`. It checks that production's telemetry
   reaches the stack, then creates the **Daily Machine Spirit** folder if needed and replaces the group's rules. Running
   it again changes nothing.

The rules stay editable in Grafana (**Alerting → Alert rules**). After changing one there, run the script with `-Export`:
it writes the group back over the file in the form the script reads, with `${prometheus}` and `${loki}` for the data
sources' uids and without what the script and Grafana add (`folderUID`, `ruleGroup`, `orgID`, ids, timestamps).
Grafana's own **More → Export** writes another format (file provisioning), which the script can't read.

## The synthetic check

Grafana Cloud's Synthetic Monitoring loads Today as a reader does, through Cloudflare, every five minutes from two
public probes (Frankfurt and London), with a k6 script (`synthetics/todays-rite.js`). The app's own telemetry can't
see what readers get: a page Cloudflare answers never reaches the app, and while the app is down, Cloudflare's
`stale-if-error` copies keep the site up, read-only. The check fails when Today doesn't answer 200, when Cloudflare
marks its copy `STALE` (it couldn't reach the app), or when Today doesn't show today's rite after 01:30 UTC, by when
publishing and its retries are over. Its results are metrics in the stack's Prometheus (`probe_all_success_sum` and
`_count`, `job="Today's rite"`), which **Today unreachable or stale** watches. **Check Today's Rite** (the workflow)
still opens an issue once a day for a missing rite.

A `STALE` copy shows only once Cloudflare's copy is older than its hour, so the check notices the app being down
within that hour, not at once.

To put it into Grafana, and after changing the script:

1. **Testing & synthetics → Synthetics → Config**: generate an access token, and save it to
   `$HOME\.machinespirit\grafana-synthetics-token.txt`. The page also shows the API's address for the stack's region.
2. Run `./tools/grafana-synthetics.ps1 -ApiUrl <that address>`. It adds the check, or updates it if it's there (found
   by its job and target). `-Probes` picks other probes by name; an unknown name lists the probes there are.
3. Run `./tools/grafana-alerts.ps1` (above) for the alert.

To try the script itself: `docker run --rm -i grafana/k6 run - < observability/synthetics/todays-rite.js`.

## The metrics

| Metric (as Prometheus names it) | Tags | What it measures |
|---|---|---|
| `dms_invocation_duration_seconds` | `function`, `outcome` | each function invocation; `outcome` is the HTTP status code, or `ok`/`failed` |
| `dms_cosmos_request_charge`, `dms_cosmos_duration_seconds` | `operation`, `status_code` | each Cosmos DB request (`CosmosMetricsHandler`); `status_code` is `failed` or `canceled` when no response came |
| `dms_generation_answers_total` | `model`, `outcome` | the models' answers: `accepted`, `refused` (broke a rule), `failed` |
| `dms_generation_answer_duration_seconds` | `model`, `asked`, `outcome` | how long a model took to answer: `asked` is `write`, `rewrite`, `distil`, `check` (a duplicate), `precept-check`, `names` (a Scribe's suggested names) or `quality` (the Augury's rubric judge); `outcome` is `answered`, `timed-out` (past its time limit) or `failed`. The measure for each time limit (the `Patience` settings in `machine-spirit.jsonc`) |
| `dms_rites_published_total` | `kind`, `model` | rites published |
| `dms_judging_runs_total` | `outcome` | judging each new draft (its quality for the Augury, its scores for More rites): `judged`, `failed`, `off` (Jev is off: the draft has its quality, but no scores) |
| `dms_generation_duplicate_checks_total` | `outcome` | checking each new draft repeats no rite already written (`DuplicateCheck`): `unique`, `repeated` (it was written again, or kept marked once its rewrites ran out), `failed` (the model couldn't tell; the draft is kept unchecked) |
| `dms_scriptorium_refusals_total` | `reason` | Scriptorium requests whose Access token was refused: the check that failed (`SecurityTokenExpiredException`, ...), `keys-unavailable`, or `no-subject` (a valid token naming nobody) |
| `dms_signins_total` | `provider`, `outcome` | readers signing in to vote, by provider (`github`): `signed-in`, `declined` (at the provider), `refused` (a callback not started in that browser, or for another provider), `failed` (anything else: the provider sent back an error such as `redirect_uri_mismatch` or no code, refused the code, or didn't give the account's id) |
| `dms_pages_purges_total` | `outcome` | pages purged from Cloudflare's cache after a vote or a new rite changed them: `purged`, `failed` (the page then refreshes within its hour at the edge) |
| `http_client_*`, `dotnet_*` | | .NET's own: outgoing HTTP calls, memory, garbage collection, the thread pool |

No tag says anything about a visitor. The Functions host exports nothing: its request telemetry would carry user agents.
