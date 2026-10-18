# Observability

The app exports its logs, traces and metrics to Grafana Cloud over OTLP (`Program.cs`; the settings come from `infra/`).
Each environment is its own service, named after its Functions app (`machinespirit-app-staging`).

## The dashboard

`grafana/daily-machine-spirit.jsonc`: traffic, Cosmos DB, the daily rite and the backlog, .NET and logs. To add or update it, in
Grafana: **Dashboards → New → Import**, and upload the file. It keeps its uid, so importing a newer version replaces the
old one. Change it in Grafana if you like, then export it (Share → Export, without "Export for sharing externally") back
over this file, so the repository stays the truth.

- **Its Metrics and Logs pickers offer only the stack's `grafanacloud-*-prom` and `grafanacloud-*-logs`,** as
  `tools/grafana-alerts.ps1` picks them. The stack has other Loki data sources (alert-state history, usage insights)
  that sort first: an unfiltered picker can choose one of them, and the log panels then show no data.
- **A regular expression in a LogQL query goes in backticks** (`` scope_name=~`Generation\..*` ``): in double quotes,
  `\.` is an escape LogQL doesn't know, and the query fails to parse.

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
| **No telemetry from production** | nothing for 26 hours: the midnight timer wakes the app every day, so silence means a broken export or a dead app |

Rare events are counted from logs, not metrics: each process keeps its own counters, and the app's processes come and
go with traffic, so a counter's first value in a process (often its only one, for something that happens once a day)
is never seen as an increase. Each log line is an event of its own.

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

## The metrics

| Metric (as Prometheus names it) | Tags | What it measures |
|---|---|---|
| `dms_invocation_duration_seconds` | `function`, `outcome` | each function invocation; `outcome` is the HTTP status code, or `ok`/`failed` |
| `dms_cosmos_request_charge`, `dms_cosmos_duration_seconds` | `operation`, `status_code` | each Cosmos DB request (`CosmosMetricsHandler`); `status_code` is `failed` or `canceled` when no response came |
| `dms_generation_answers_total` | `model`, `outcome` | the models' answers: `accepted`, `refused` (broke a rule), `failed` |
| `dms_generation_answer_duration_seconds` | `model`, `asked`, `outcome` | how long a model took to answer: `asked` is `write`, `rewrite`, `distil`, `check` (a duplicate), `precept-check` or `names` (a Scribe's suggested names); `outcome` is `answered`, `timed-out` (past its time limit) or `failed`. The measure for each time limit (the `Patience` settings in `machine-spirit.jsonc`) |
| `dms_rites_published_total` | `kind`, `model` | rites published |
| `dms_judging_runs_total` | `outcome` | judging each new draft (its quality for the Augury, its scores for More rites): `judged`, `failed`, `off` |
| `dms_generation_duplicate_checks_total` | `outcome` | checking each new draft repeats no rite already written (`DuplicateCheck`): `unique`, `repeated` (it was written again, or kept marked once its rewrites ran out), `failed` (the model couldn't tell; the draft is kept unchecked) |
| `dms_scriptorium_refusals_total` | `reason` | Scriptorium requests whose Access token was refused: the check that failed (`SecurityTokenExpiredException`, ...), `keys-unavailable`, or `no-subject` (a valid token naming nobody) |
| `dms_signins_total` | `provider`, `outcome` | readers signing in to vote, by provider (`github`): `signed-in`, `declined` (at the provider), `refused` (a callback not started in that browser, or for another provider), `failed` (anything else: the provider sent back an error such as `redirect_uri_mismatch` or no code, refused the code, or didn't give the account's id) |
| `dms_pages_purges_total` | `outcome` | pages purged from Cloudflare's cache after a vote or a new rite changed them: `purged`, `failed` (the page then refreshes within its hour at the edge) |
| `http_client_*`, `dotnet_*` | | .NET's own: outgoing HTTP calls, memory, garbage collection, the thread pool |

No tag says anything about a visitor. The Functions host exports nothing: its request telemetry would carry user agents.
