// A load test of the public pages (tests/load/README.md). Reactions are left out: they'd change the counts.
//
//   k6 run -e PROFILE=smoke tests/load/site.js
//
// PROFILE: smoke (a minute, one visitor), load (a realistic busy hour, sped up), stress (keeps climbing to find the
// ceiling), cold (one request per page, for after the app has scaled to zero). BASE_URL defaults to staging.
import http from 'k6/http';
import { check, group } from 'k6';

const baseUrl = (__ENV.BASE_URL || 'https://machinespirit-app-staging.azurewebsites.net').replace(/\/$/, '');
const profile = __ENV.PROFILE || 'smoke';

// A missing rite's 404 is an answer the test asks for, not a failure.
http.setResponseCallback(http.expectedStatuses(200, 404));

const profiles = {
  smoke: { executor: 'constant-vus', vus: 1, duration: '1m' },
  load: {
    executor: 'ramping-arrival-rate',
    startRate: 1,
    timeUnit: '1s',
    preAllocatedVUs: 20,
    maxVUs: 100,
    stages: [
      { target: 10, duration: '2m' },
      { target: 10, duration: '5m' },
      { target: 0, duration: '1m' },
    ],
  },
  stress: {
    executor: 'ramping-arrival-rate',
    startRate: 5,
    timeUnit: '1s',
    preAllocatedVUs: 50,
    maxVUs: 400,
    stages: [
      { target: 25, duration: '2m' },
      { target: 50, duration: '2m' },
      { target: 100, duration: '2m' },
      { target: 200, duration: '2m' },
      { target: 0, duration: '1m' },
    ],
  },
  cold: { executor: 'per-vu-iterations', vus: 1, iterations: 1 },
};

if (!profiles[profile]) throw new Error(`Unknown PROFILE "${profile}": use ${Object.keys(profiles).join(', ')}.`);

export const options = {
  scenarios: { [profile]: profiles[profile] },
  // Read only for "cold": the first request after scaling to zero is the point there, not something to hold to a limit.
  thresholds: profile === 'cold' ? {} : {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{page:today}': ['p(95)<500'],
    'http_req_duration{page:rite}': ['p(95)<500'],
    'http_req_duration{page:archive}': ['p(95)<500'],
    checks: ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

// The rites the archive lists, so the test reads real rite pages and real older archive pages.
export function setup() {
  const archive = http.get(`${baseUrl}/archive`, { tags: { page: 'setup' } });
  const today = http.get(`${baseUrl}/`, { tags: { page: 'setup' } });
  if (archive.status !== 200 || today.status !== 200)
    throw new Error(`The site isn't answering: /archive ${archive.status}, / ${today.status}.`);
  // Today names its own rite in its share link (data-url); the archive links to the older ones.
  const numbers = [...`${today.body}${archive.body}`.matchAll(/(?:href|data-url)="\/r\/(\d+)"/g)].map((match) => Number(match[1]));
  return { rites: [...new Set(numbers)].sort((a, b) => b - a) };
}

const pick = (items) => items[Math.floor(Math.random() * items.length)];

const get = (path, page, expected) => {
  const response = http.get(`${baseUrl}${path}`, { tags: { page } });
  check(response, { [`${page} answers ${expected}`]: (r) => r.status === expected });
  return response;
};

// One visitor's request, weighted roughly as visits would be: mostly Today, then rite pages (shared links), the
// archive, and the odd wrong address. The fonts are cached by browsers for a year, so they barely show up.
export default function (data) {
  if (profile === 'cold') {
    for (const [path, page, expected] of [['/', 'today', 200], ['/archive', 'archive', 200], [`/r/${data.rites[0] ?? 1}`, 'rite', 200], ['/healthz', 'health', 200]])
      group(page, () => get(path, page, expected));
    return;
  }

  const roll = Math.random();
  if (roll < 0.45) get('/', 'today', 200);
  else if (roll < 0.75 && data.rites.length > 0) get(`/r/${pick(data.rites)}`, 'rite', 200);
  else if (roll < 0.9) get(data.rites.length > 1 && Math.random() < 0.5 ? `/archive?before=${pick(data.rites)}` : '/archive', 'archive', 200);
  else if (roll < 0.95) get('/r/999999', 'missing', 404);
  else if (roll < 0.98) get('/no-such-page', 'missing', 404);
  else get('/fonts/eb-garamond.woff2', 'font', 200);
}
