# Who should judge drafts for the Augury? (#10)

A one-off blind test, outside the app: nothing from it ships unless the result says so.

**The judges:**
- **Jev:** its quality question, as the Augury uses now, stored with each draft.
- **Rubric:** the other model (`gpt-6-luna` judges `gpt-6-sol`'s drafts and the other way round) with a short rubric:
  funny, an accurate Heretical Truth, original, within the rules. A score from 1 to 10.
- **Taught:** the same model and rubric, plus the Precepts and the Scribes' 20 newest judgments (never one of the
  draft being judged).

The Decisions API (OpenAI's own, `gpt-6-luna` only) was considered and left out for now: it isn't on Azure, so it
would need an OpenAI key.

**Steps** (from the repository's root, signed in with `az login`; reads staging's database alone):
1. `dotnet run experiments/blind-judges/judges.cs -- score`: each of staging's judged drafts, scored by both model
   judges, into `results/scores.json`.
2. `dotnet run experiments/blind-judges/judges.cs -- pairs`: for each two judges, the ten pairs of drafts they order
   most differently, sides shuffled. `results/pairs.json` keeps which judge preferred which side; `page-pairs.json`
   has the rites alone, for the page.
3. The owner picks the better rite of each pair on a private page (`page.template.html`, the pairs put in its
   `PAIRS`), without seeing the judges or their scores: left, right, or about the same.
4. The tally: for each judge, how often it preferred the rite the owner picked.
5. `dotnet run experiments/blind-judges/judges.cs -- tally`: with the picks saved from the page's database in
   `results/picks-raw/picks/`.

## The result (2026-10-19)

34 of staging's 35 judged drafts (one had no valid score from the taught judge), 30 pairs; the owner found a better
rite in 26, and 4 about the same.

| Judge | Agreed with the owner |
|---|---|
| Jev | 10 of 25 (40%) |
| Rubric | 14 of 23 (61%) |
| Taught | 14 of 25 (56%) |

Head to head, where two judges preferred opposite rites: Jev 5, rubric 11; Jev 8, taught 12; rubric 6, taught 5.

- **Jev's quality loses to a model judge,** and agrees with the owner less often than a coin would. Its scores barely
  spread (0.58 to 0.76 over 35 drafts), so it hardly orders the calendar at all.
- **Teaching made no difference here,** but staging's Precepts and decisions are mostly test actions, not the Scribes'
  taste, so this says little about production's.
- **Small and noisy:** 16 pairs between Jev and the rubric judge is suggestive, not proof, and the model judges scored
  the same draft up to 2 points apart across two runs. A judge that ships should average a few scores per draft.
