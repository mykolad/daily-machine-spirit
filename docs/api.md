# The rites API

Every rite The Daily Machine Spirit has published, for anyone to read: `GET https://dailymachinespirit.fyi/api/v1/rites`.
As on the site, each rite's **Heretical Truth** stays sealed until it's asked for, one rite at a time:
`GET https://dailymachinespirit.fyi/api/v1/rites/{number}/heretical-truth`.

- **Read-only and open:** no sign-in, no key, and nothing about the caller is kept. Any site's page may read it
  (`Access-Control-Allow-Origin: *`).
- **One address for each answer, cached:** Cloudflare keeps each answer until a new rite is published or a rite's
  words are erased, so ask as often as you need. Any query string is redirected (301) to the same address without it:
  nothing is filtered on the server, so whatever a client is looking for never leaves it.
- **Versioned in the path:** `v1` keeps its fields, their names and their meaning. A change that would break a client
  comes as `v2`, beside it. Fields may be added; a client ignores what it doesn't know.

## The rites

`GET /api/v1/rites`: `200` with `application/json`:

```json
{
  "rites": [
    {
      "number": 12,
      "day": "2026-10-14",
      "kind": "ritual",
      "title": "The Rite of Re-Run",
      "text": "When the trial fails, press Re-run thrice, intoning `it passed locally`.",
      "ailment": "",
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
- `ailment`: optional. The trouble the rite is for, in a line, without its fix ("a test that fails at random and passes
  when re-run"): the best thing to match a trouble on. Empty when the rite has none, as every rite does for now.
- `subjects`: what it's filed under, from the site's own vocabulary; `key` is the subject's page
  (`/tags/<key>`). Empty for a rite filed under nothing.
- `url`: the rite's page, where its Heretical Truth is a click away.

## A rite's Heretical Truth

`GET /api/v1/rites/{number}/heretical-truth`: `200` with `application/json`:

```json
{
  "number": 12,
  "hereticalTruth": "A test that passes on a rerun depends on timing or order. Find what it shares with another test."
}
```

- `hereticalTruth`: one or two plain sentences on what really happens, and what to do instead. Backticks and erased
  words as in the rites.

`404` when no rite has that number (not cached: it may be published tomorrow).

## Errors

`503` when the rites can't be read just now (not cached): try again later.

## Clients

- **Consult the Machine Spirit** (`skills/consult-the-machine-spirit`): a skill that prescribes a rite for a
  developer's trouble. It fetches the list and lets the user's own model choose, so the trouble never leaves their
  machine, then reveals the rite's Heretical Truth only if the user dares to hear it.
