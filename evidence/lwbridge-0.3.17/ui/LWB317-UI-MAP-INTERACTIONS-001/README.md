# LWB317-UI-MAP-INTERACTIONS-001 evidence

Per-milestone evidence for the campaign in
`docs/work-items/LWB317-UI-MAP-INTERACTIONS-001.md`. Original asset identity is
unchanged: `MapDataPanel-B4GXEND2.js` SHA-256
`ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`.

## Milestone A — request lifetime (this section is complete)

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

Result: baseline 0 failures (38 scenario runs, incl. 12 tab-cache-agnostic scenarios);
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
