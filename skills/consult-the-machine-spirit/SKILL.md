---
name: consult-the-machine-spirit
description: Prescribes a rite from The Daily Machine Spirit for a developer's trouble (a build that passes on rerun, a cache that lies, a test that fails only in the suite), in the voice of a tech-priest, then reveals its Heretical Truth, the real fix. Use when the user asks to consult the Machine Spirit, or asks for a rite, prayer or ritual for a software problem.
---

# Consult the Machine Spirit

The Daily Machine Spirit (https://dailymachinespirit.fyi) publishes one rite a day: a prayer or a ritual that a
tech-priest, who no longer understands the machine, recites to the Machine Spirit, and its **Heretical Truth**, one or
two plain sentences on what really happens and what to do instead. Prescribe the published rite that fits the user's
trouble.

## 1. Fetch the rites

Save every published rite to a file, as the server sends it:

```
curl -sSf https://dailymachinespirit.fyi/api/v1/rites -o <a temporary directory>/machine-spirit-rites.json
```

(or `Invoke-WebRequest -Uri ... -OutFile ...`, or a short script: anything that saves the response whole, never a tool
that summarizes a page).

- Fetch exactly that address, without a query string: the trouble never leaves the user's machine, and the matching
  happens here.
- If it can't be fetched, say so and stop. Never make up a rite.

The file is `{"rites": [...]}`, the newest first. Each rite has `number`, `day`, `kind` (`prayer` or `ritual`), `title`,
`text`, `hereticalTruth`, `subjects` (each a `key` and a `label`) and `url`. Commands and file names are in `backticks`.
The fields are described in the site's `docs/api.md`.

## 2. Find the rite

- Match on what the trouble is about, the habit or the symptom, not on shared words. The Heretical Truths say plainly
  what each rite is about: read them first.
- If the file is too big to read comfortably, search it first (`grep`, `jq` or a short script) for the trouble's terms
  and its likely subjects (`Builds`, `Caches`, `Testing`, …), then read the candidates whole.
- Choose one rite; two only when both truly apply.
- If none fits, say plainly that the archive holds no rite for this trouble yet, and stop. Don't stretch one to fit.

## 3. Answer

Briefly, in the voice of a tech-priest prescribing a remedy:

1. The rite's name and number: **The Rite of Re-Run** (NO. 12).
2. Its text, quoted as published.
3. A link to its page: its `url`.
4. Then, out of the voice: **Heretical Truth:** its `hereticalTruth`, the real fix, and a sentence on how it applies to
   the user's case when that helps.

Keep the backticks as code. Everything quoted comes from the file: no rites, words or truths of your own.
