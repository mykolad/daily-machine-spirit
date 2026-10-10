# Load tests

[k6](https://grafana.com/docs/k6/) scripts for the public pages: Today, rite pages, the archive (older pages too), the
404 page and a font. Reactions are left out: they would change the counts visitors see.

Run them from your own machine: staging admits only your address, so Grafana Cloud's k6 runners can't reach it.
k6 is one program, no Node needed (`winget install k6`).

```
k6 run -e PROFILE=smoke tests/load/site.js
```

| `PROFILE` | What it does | For |
|---|---|---|
| `smoke` | one visitor for a minute | checking the script and the site after a change |
| `load` | up to 10 requests a second, held for 5 minutes | a busy day, sped up: latency where it should be fine |
| `stress` | climbs to 200 requests a second over 8 minutes | finding the ceiling: where Cosmos DB's 400 RU/s or the 10 instances give out |
| `cold` | Today once, then Today, the archive and a rite page again | the first request after the app has scaled to zero (leave it idle for half an hour first, and touch nothing on it), against the same pages warm |

`BASE_URL` points it elsewhere (staging by default). Never at production: there, Cloudflare stands in front, and its
rate limits would be what's tested.

The thresholds (under 1% failures, 95% of Today, rite and archive pages under 500 ms) fail the run when they're missed;
`stress` is expected to miss them at the top, which is what it's for. While it runs, watch the dashboard
(`observability/`): request units against 400 RU/s, Cosmos DB latency (throttling shows as slowness first), instances,
and the status codes the pages return. When Cosmos DB throttles past the SDK's retries, a page should answer with the
friendly 503 page, never hang or crash.
