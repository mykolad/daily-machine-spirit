# Observability

The app exports its logs, traces and metrics to Grafana Cloud over OTLP (`Program.cs`; the settings come from `infra/`).
Each environment is its own service, named after its Functions app (`machinespirit-app-staging`).

## The dashboard

`grafana/daily-machine-spirit.json`: traffic, Cosmos DB, the daily rite and the backlog, .NET and logs. To add or update it, in
Grafana: **Dashboards → New → Import**, upload the file, and pick the stack's Prometheus and Loki data sources. It keeps
its uid, so importing a newer version replaces the old one. Change it in Grafana if you like, then export it (Share →
Export, without "Export for sharing externally") back over this file, so the repository stays the truth.

## The alerts

Production only (`job="machinespirit-app"`), so staging's experiments never wake anyone. Each one's description says
what to look at.

| Alert | Fires when |
|---|---|
| **No rite today** | Today doesn't show today's rite at 02:00 UTC, after the timer and its retries. A workflow (**Check Today's Rite**), not Grafana: it reads the page readers get, through Cloudflare's cache, and opens an issue. Before launch the slumbering page passes. |
| **Refills failing** | a backlog refill failed all three tries within an hour |
| **Backlog ran empty** | publishing found no waiting draft more than once in a week (`dms_backlog_empty_total`) |
| **Jev failing** | more than half the drafts of the last three hours couldn't be judged |
| **Server errors** | more than 5% of requests answered 5xx for ten minutes, over at least ten requests |
| **No telemetry from production** | nothing for 26 hours: the midnight timer wakes the app every day, so silence means a broken export or a dead app |

The Grafana rules are `grafana/alerts.json`, one rule group, evaluated every five minutes, and they notify through the
stack's default notification policy (the default contact point emails the stack's owner). To put them into Grafana:

1. **Administration → Users and access → Service accounts → Add service account** (role **Editor**), then **Add service
   account token**. Save the token to `$HOME\.machinespirit\grafana-alerts-token.txt`.
2. Run `./tools/grafana-alerts.ps1 -GrafanaUrl https://<stack>.grafana.net`. It checks that production's telemetry
   reaches the stack, then creates the **Daily Machine Spirit** folder if needed and replaces the group's rules. Running
   it again changes nothing.

The rules stay editable in Grafana (**Alerting → Alert rules**). After changing one there, export the group (**More →
Export**, JSON) back over the file, keeping its form: `${prometheus}` for the data source's uid, without the
`folderUID`, `ruleGroup` and `orgID` the script adds.

## The metrics

| Metric (as Prometheus names it) | Tags | What it measures |
|---|---|---|
| `dms_invocation_duration_seconds` | `function`, `outcome` | each function invocation; `outcome` is the HTTP status code, or `ok`/`failed` |
| `dms_cosmos_request_charge`, `dms_cosmos_duration_seconds` | `operation`, `status_code` | each Cosmos DB request (`CosmosMetricsHandler`); `status_code` is `failed` or `canceled` when no response came |
| `dms_generation_answers_total` | `model`, `outcome` | the models' answers: `accepted`, `refused` (broke a rule), `failed` |
| `dms_rites_published_total` | `kind`, `model` | rites published |
| `dms_backlog_empty_total` | | publishing found no waiting draft, and wrote the day's rite on the spot, unmoderated |
| `dms_judging_runs_total` | `outcome` | judging each new draft (its quality for the Augury, its scores for More rites): `judged`, `failed`, `off` |
| `dms_scriptorium_refusals_total` | `reason` | Scriptorium requests whose Access token was refused: the check that failed (`SecurityTokenExpiredException`, ...), or `keys-unavailable` |
| `dms_signins_total` | `provider`, `outcome` | readers signing in to vote, by provider (`github`): `signed-in`, `declined` (at the provider), `refused` (a callback not started in that browser, or for another provider), `failed` (anything else: the provider sent back an error such as `redirect_uri_mismatch` or no code, refused the code, or didn't give the account's id) |
| `http_client_*`, `dotnet_*` | | .NET's own: outgoing HTTP calls, memory, garbage collection, the thread pool |

No tag says anything about a visitor. The Functions host exports nothing: its request telemetry would carry user agents.
