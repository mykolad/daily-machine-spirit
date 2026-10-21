# The rites API

Every rite The Daily Machine Spirit has published, for anyone to read: `GET https://dailymachinespirit.fyi/api/v1/rites`.

- **Read-only and open:** no sign-in, no key, and nothing about the caller is kept. Any site's page may read it
  (`Access-Control-Allow-Origin: *`).
- **One address, cached:** Cloudflare keeps the answer until a new rite is published or a rite's words are erased, so
  ask as often as you need. Any query string is redirected (301) to the one address: nothing is filtered on the server,
  so whatever a client is looking for never leaves it.
- **Versioned in the path:** `v1` keeps its fields, their names and their meaning. A change that would break a client
  comes as `v2`, beside it. Fields may be added; a client ignores what it doesn't know.

## The answer

`200` with `application/json`:

```json
{
  "rites": [
    {
      "number": 12,
      "day": "2026-10-14",
      "kind": "ritual",
      "title": "The Rite of Re-Run",
      "text": "When the trial fails, press Re-run thrice, intoning `it passed locally`.",
      "hereticalTruth": "A test that passes on a rerun depends on timing or order. Find what it shares with another test.",
      "subjects": [{ "key": "testing", "label": "Testing" }],
      "url": "https://dailymachinespirit.fyi/r/12"
    }
  ]
}
```

- `rites`: every published rite, the newest first.
- `number`: the rite's number, NO. 12 on the site.
- `day`: the day it was published (UTC), `yyyy-MM-dd`.
- `kind`: `prayer` or `ritual`.
- `title`, `text`: as published. Commands, flags and file names are in `backticks`. A word erased from a rite (as if lost
  to time) is an emoji, there as on the site.
- `hereticalTruth`: one or two plain sentences on what really happens, and what to do instead.
- `subjects`: what it's filed under, from the site's own vocabulary; `key` is the subject's page
  (`/tags/<key>`). Empty for a rite filed under nothing.
- `url`: the rite's page.

`503` when the rites can't be read just now (not cached): try again later.

## Clients

- **Consult the Machine Spirit** (`skills/consult-the-machine-spirit`): a skill that prescribes a rite for a
  developer's trouble. It fetches this list and lets the user's own model choose, so the trouble never leaves their
  machine.
