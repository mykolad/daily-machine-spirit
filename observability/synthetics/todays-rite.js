// What a reader gets at Today, through Cloudflare, as Grafana's Synthetic Monitoring checks it every 15 minutes
// (tools/grafana-synthetics.ps1, observability/README.md). It fails when Today doesn't answer, when Cloudflare serves a
// stale copy because it couldn't reach the app, or when Today doesn't show today's rite.
import http from 'k6/http';
import { check, fail } from 'k6';

// The rite is published at 00:00 UTC, and a failed run is tried again twice, 20 minutes apart: until 01:30 UTC
// yesterday's rite still counts.
const minutesOfGraceAfterMidnight = 90;

export default function () {
  const response = http.get('https://dailymachinespirit.fyi/');
  const shown = /class="rite-date" datetime="(\d{4}-\d{2}-\d{2})"/.exec(response.body || '');
  const now = new Date();
  const today = now.toISOString().slice(0, 10);
  const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  const inGrace = now.getUTCHours() * 60 + now.getUTCMinutes() < minutesOfGraceAfterMidnight;

  const passed = check(response, {
    'Today answers 200': (r) => r.status === 200,
    // STALE: Cloudflare's copy had expired and the app didn't answer, so readers got the old copy (stale-if-error).
    'Cloudflare reached the app': (r) => r.headers['Cf-Cache-Status'] !== 'STALE',
    "Today shows today's rite": () => shown !== null && (shown[1] === today || (inGrace && shown[1] === yesterday)),
  });
  if (!passed) {
    fail(`Today: ${response.status}, cf-cache-status ${response.headers['Cf-Cache-Status']}, rite of ${shown ? shown[1] : 'no day'}`);
  }
}
