# LWB317-UI-MAP-INTERACTIONS-001 evidence

Per-milestone evidence for the campaign in
`docs/work-items/LWB317-UI-MAP-INTERACTIONS-001.md`. Original asset identity is
unchanged: `MapDataPanel-B4GXEND2.js` SHA-256
`ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`.

## Milestone A — request lifetime

Defect (PM-025): NAVIGATION-001 replaced the search effect's local cancellation flag
with a generation counter and returned no cleanup, so a request whose effect had been
disposed (backend loss, unmount) could still write rows/total/error/loading.

Correction (`MapDataPage.jsx` search effect): the effect now returns
`() => { searchGeneration.current += 1; }`. Disposal retires the request through the same
generation that tab and server transitions already advance, so the tab-cache correction and
generation ordering of NAVIGATION-001 are retained unchanged.

| Artifact | Meaning |
|---|---|
| `harness.mjs` | New replay adapter. Reads the import declarations of the page source it is given, binds relative `.js` imports to the real modules, stubs `.jsx` components and provides a persistent hook runtime (React-ordered cleanup-then-effect commit, unmount, props replacement, controlled clock). It evaluates unmodified production source text. |
| `delivery-305240e.MapDataPage.jsx` | Immutable copy of the component reviewed in PM-025 (`git show 305240e:...`). |
| `check-request-lifetime.mjs` / `request-lifetime-results.json` | 13 scenarios x {pre-navigation baseline, delivery 305240e, current}. Passing = state identical before and after the obsolete reply. |
| `check-navigation-replay.mjs` / `navigation-replay-results.json` | Historical NAVIGATION-001 `campaign()` text sliced from the immutable script at run time and replayed through `harness.mjs`; baseline reproduces the same six recorded defects, current has none. |
| `check-protected-wip.mjs` | Compares the pinned protected-WIP manifest; verifies nothing protected is staged. |

Result: 13 scenarios x 3 sources = 38 runs (the Scheduled-entry scenario is skipped for the pre-navigation baseline, which has no Scheduled boundary); baseline 0 failures;
delivery 305240e fails 4 (backend-loss success, backend-loss rejection, unmount success,
unmount rejection); current 0 failures. The lead's original reproduction
(`../LWB317-PM-025/check-availability.mjs`, results file untouched and preserved) now reports
zero current failures when run without `--record`.

Covered disposal routes: backend loss, effect cleanup/unmount, provider replacement
(success and rejection), re-entry (stale success while re-entered request pending, stale
rejection after the re-entered request succeeded), stale `finally`, effect re-run, server
transition, Scheduled Plunder entry; plus a positive control that the current request still
applies after recovery.

Limits: synthetic deferred local responses through the hook adapter. Not browser or native
evidence. Loading after backend loss is intentionally unchanged from the baseline (a request
retired by backend loss leaves `loading` as it was; the next availability re-entry starts a
new search that owns it).

## Milestone B — search and selection

The original component `R` is executed from the asset bytes (`original/`, produced by an independent
reviewer: `original/contract.md` 88 locators, `original/results.json` 59 scenarios / 145 claims,
`original/negative-cases.md` 51 cases, `original/canonical-differential.json`). `scenarios.mjs` +
`driver.mjs` + `flavors.mjs` run 38 user-level scenarios against the original, production and the immutable
pre-campaign page `baseline-3e8617c.MapDataPage.jsx`; `interaction-results.json` records every observation.
Production equals the original in 38/38; the baseline differs in 31. `check-mutations.mjs` proves 14/14
deliberate defects are detected. Documented non-reproduced differences are listed in
`docs/reviews/2026-10-02-LWB317-UI-MAP-INTERACTIONS-001.md`.

## Milestone C — Scheduled Plunder presentation

`scheduled/` (differential render of the ACTUAL original `ot`/`st`, 10,482 renders, 51/51 mutations; see
`scheduled/README.md`). Fixtures live in `src/LWBridge.UI-0.3.17/src/mapPlunderFixtures.js` and are wired
by `mapPreviewApi.js` for `map-scheduled`, `map-scheduled-populated`, `map-scheduled-conditional` and
`map-actions-*` only.

## Milestone D — integration

`check-integration.mjs` (14 production callback/effect scenarios; `integration-baseline-results.json` keeps
the 11 baseline failures), `replay-historical.mjs` (historical regressions), `browser-results.json` +
`screenshots/` (offline preview, worker-run), `coverage-matrix.md`, `validate-evidence.mjs`.

## Reproduce

```
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-navigation-replay.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-mutations.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-integration.mjs            # --source=baseline expects FAIL
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/replay-historical.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/scheduled/check-scheduled-plunder.mjs   # ~5 minutes
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/check-original-runtime.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/validate-evidence.mjs
```

`--record` rewrites only this directory's result files; never run the historical scripts directly with
`--record` (the replay adapter does not pass it).

## Limits

Project-lead browser recheck is recorded separately under `../LWB317-PM-026/`.
It supersedes BR6's pre-final-translation message without rewriting the historical
worker browser observations. See the PM-026 review for the final decision.

Synthetic deferred local replies and controlled clocks (hook-adapter evidence), offline preview (browser
evidence). Not original pixel, native, persistence or gameplay evidence. Preview stays `online:false`.
