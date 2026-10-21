---
name: consult-the-machine-spirit
description: Prescribes a rite from The Daily Machine Spirit for a developer's trouble (a build that passes on rerun, a cache that lies, a test that fails only in the suite), in the voice of a tech-priest, then offers to reveal its Heretical Truth, the real fix. Use when the user asks to consult the Machine Spirit, or asks for a rite, prayer or ritual for a software problem.
---

# Consult the Machine Spirit

The Daily Machine Spirit (https://dailymachinespirit.fyi) publishes one rite a day: a prayer or a ritual that a
tech-priest, who no longer understands the machine, recites to the Machine Spirit. Each rite has a sealed **Heretical
Truth**, one or two plain sentences on what really happens and what to do instead. Prescribe the published rite that
fits the user's trouble, then let the user decide whether to break the seal.

## 1. Fetch the rites

Save every published rite to a file, as the server sends it:

```
curl -sSf https://dailymachinespirit.fyi/api/v1/rites -o <a temporary directory>/machine-spirit-rites.json
```

(or `Invoke-WebRequest -Uri ... -OutFile ...`, or a short script: anything that saves the response whole).

- **If no command can reach the site** (a sandbox that only reaches some domains, as in the Claude app), fetch the same
  address with a web fetch tool instead, one that returns the response as the server sent it. Never one that
  summarizes or answers questions about the page: it would drop rites, or change their words.
- Fetch exactly that address, without a query string: the trouble is never sent to the site, and the matching
  happens here.
- If it can't be fetched either way, say so and stop. Never make up a rite. In the Claude app, the user can add
  `dailymachinespirit.fyi` to the allowed domains (Settings → Capabilities) for the commands to reach it.

The answer is `{"rites": [...]}`, the newest first. Each rite has `number`, `day`, `kind` (`prayer` or `ritual`), `title`,
`text`, `ailment`, `subjects` (each a `key` and a `label`) and `url`. Commands and file names are in `backticks`. The
Heretical Truths aren't in it. The fields are described in the site's `docs/api.md`.

## 2. Find the rite

- Match on what the trouble is about, the habit or the symptom, not on shared words. A rite's `ailment`, when it isn't
  empty, says plainly what trouble it's for: read those first. Otherwise read the titles and texts for the habit they
  mock (pressing Re-run, clearing a cache, adding a sleep), and the subjects.
- If the file is too big to read comfortably, search it first (`grep`, `jq` or a short script) for the trouble's terms
  and its likely subjects (`Builds`, `Caches`, `Testing`, …), then read the candidates whole.
- Choose one rite; two only when both truly apply.
- If none fits, say plainly that the archive holds no rite for this trouble yet, and stop. Don't stretch one to fit.

## 3. Prescribe it

Briefly, in the voice of a tech-priest prescribing a remedy:

1. The rite's name and number: **The Rite of Re-Run** (NO. 12).
2. Its text, quoted as published.
3. A link to its page: its `url`.
4. Last, a question: does the user dare to hear the rite's Heretical Truth? Say it in the voice, and stop there.

Keep the backticks as code. Everything quoted comes from the file: no rites or words of your own. Don't guess at the
truth or hint at the fix: the seal is the point.

## 4. Reveal the truth, only if asked

If the user agrees, fetch that rite's truth, and only that rite's (both, if you prescribed two):

```
curl -sSf https://dailymachinespirit.fyi/api/v1/rites/<number>/heretical-truth -o <a temporary directory>/machine-spirit-truth.json
```

(or with the web fetch tool, as in step 1, if commands can't reach the site).

It's `{"number": 12, "hereticalTruth": "..."}`. Then, out of the voice: **Heretical Truth:** its `hereticalTruth`, the
real fix, and a sentence on how it applies to the user's case when that helps.

- Never fetch a truth before the user has agreed: what you know, you may let slip.
- If the user declines, the seal stays: no truth, and no hint of it. They can still open the rite's page.
- If the truth can't be fetched, say so; never write one of your own.
