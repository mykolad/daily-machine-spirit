# Observability

The app exports its logs, traces and metrics to Grafana Cloud over OTLP (`Program.cs`; the settings come from `infra/`).
Each environment is its own service, named after its Functions app (`machinespirit-app-staging`).

## The dashboard

`grafana/daily-machine-spirit.json`: traffic, Cosmos DB, the daily rite and the backlog, .NET and logs. To add or update it, in
Grafana: **Dashboards → New → Import**, upload the file, and pick the stack's Prometheus and Loki data sources. It keeps
its uid, so importing a newer version replaces the old one. Change it in Grafana if you like, then export it (Share →
Export, without "Export for sharing externally") back over this file, so the repository stays the truth.

## The metrics

| Metric (as Prometheus names it) | Tags | What it measures |
|---|---|---|
| `dms_invocation_duration_seconds` | `function`, `outcome` | each function invocation; `outcome` is the HTTP status code, or `ok`/`failed` |
| `dms_cosmos_request_charge`, `dms_cosmos_duration_seconds` | `operation`, `status_code` | each Cosmos DB request (`CosmosMetricsHandler`); `status_code` is `failed` or `canceled` when no response came |
| `dms_generation_answers_total` | `model`, `outcome` | the models' answers: `accepted`, `refused` (broke a rule), `failed` |
| `dms_rites_published_total` | `kind`, `model` | rites published |
| `dms_scoring_runs_total` | `outcome` | scoring each new draft (for the Augury and More rites): `scored`, `failed`, `off` |
| `http_client_*`, `dotnet_*` | | .NET's own: outgoing HTTP calls, memory, garbage collection, the thread pool |

No tag says anything about a visitor. The Functions host exports nothing: its request telemetry would carry user agents.
