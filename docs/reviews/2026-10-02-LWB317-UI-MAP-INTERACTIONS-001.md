# LWB317-UI-MAP-INTERACTIONS-001 — worker delivery

Worker submission status: AWAITING_REVIEW at 5b76ae8. **Project lead PM-026 now
accepts the focused source/local UI scope** after independent continuation and
verification; see its separate closeout review. This file records the worker
implementation handoff for the large UI campaign. Evidence packet:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/` (README, coverage matrix,
per-milestone results, validator). Scope is UI/UX only: no native provider, scan,
scheduling, sharing, cancel, claim or gameplay operation was added or executed.

Original asset identity unchanged: `MapDataPanel-B4GXEND2.js` SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`. All locators
below are UTF-8 byte ranges in that asset (full list in `original/contract.md`).

## Milestones and commits

| Milestone | Outcome | Commit |
|---|---|---|
| A request lifetime | complete | `3e8617c` (pushed, remote verified) |
| B search and selection | complete for the assigned local behaviors; documented differences below | `c57be79` |
| C Scheduled Plunder presentation | complete as canonical presentation + offline fixtures; runtime producer UNKNOWN/BLOCKED | `c57be79` |
| D integration and verification | complete; consolidated evidence and docs | final documentation commit (see the delivery message for the SHA) |

## A — request lifetime (PM-025 regression)

The original asset has no local cancel flag (`rr` 38513-39238 only compares a monotonic
generation); the pre-navigation canonical page had one. NAVIGATION-001 dropped the
disposal cleanup, so a request whose effect had been disposed could still write
rows/total/error/loading. Correction: the search effect returns
`() => { searchGeneration.current += 1; }`, retiring the request through the same
generation tab and server transitions already advance. The tab-cache correction is untouched.

* `check-request-lifetime.mjs`: 13 scenarios x {baseline, 305240e, current} = 38 runs via the new
  generic hook adapter (`harness.mjs`). Backend loss, unmount, provider replacement, re-entry,
  stale success/rejection/finally, effect re-run, server transition, Scheduled entry, positive
  control. **Baseline 0 failures, 305240e 4 failures (backend loss success/rejection, unmount
  success/rejection), current 0.**
* `check-navigation-replay.mjs` replays the historical `campaign()` text (sliced from the immutable
  script) through the adapter: baseline reproduces the same 6 recorded defects, current 0.
* The lead's `PM-025/check-availability.mjs` now reports 0 current failures; its recorded results
  file and the NAVIGATION-001 scripts/results are unchanged (validator compares with `bec0910`).

## B — search and selection (recovered, with the original executed as oracle)

Method: the original component `R` is executed from the asset bytes in a persistent hook runtime
(`original/original-runtime.mjs`, written by an independent subagent who recovered
`original/contract.md` without seeing this worker's reading; the two readings agreed, and the
subagent's executed comparison found one real defect — the action message was not cleared on tab
change — fixed before delivery). The same 38 user-level scenarios then run through one shared driver
against the original, production and the immutable pre-campaign baseline
(`baseline-3e8617c.MapDataPage.jsx`): **38/38 production observations equal the original; the baseline
differs in 31.** 14 deliberate defects in a copy of the production page are all detected.

Recovered and implemented:

* **Keyword**: raw untrimmed string, shared by tabs; not a search-effect dependency (34816-34869); there
  is no debounce, throttle or timer anywhere in the search path (6 timer sites in the whole asset, none
  in it). Typing never searches. The query uses whatever text is in the input whenever any dependency
  changes (filter, sort, page, tab, connection) — typed-but-unsubmitted text included.
* **Search button** (54327-54407): never disabled; page 1 searches directly, page > 1 only resets to page 1;
  clicking while loading starts a newer request. Canonical runtime fence kept: disabled when the backend
  or data server is unavailable.
* **Names** (50967-51415): selecting clears the keyword and resets the page; typing drops the selected
  name (that first keystroke searches); Resource and Monster selections are independent and survive tab
  changes; option text is the recovered game text or the raw key plus ` (count)`; label `common.name`;
  a name the refreshed options no longer offer is dropped.
* **Selection** (31301-31353, 34121-34182, 39593-39866, 17072-17832): Dispatch and Ghost share one map
  keyed `serverId:uuid` holding the toggled row plus `taskKind`; Truck has its own map. The stored key is
  untrimmed while the checkbox lookup is trimmed (a padded uuid is counted but never shows checked). The
  Dispatch/Ghost map is emptied on every tab change (also Dispatch<->Ghost); the Truck map is not.
  Neither is touched by page, filter, search, refresh or server change; clear-data empties both. Counts are
  the number of stored keys, not on-screen rows.
* **Random delay** (32925-32959, 55299-55431): raw string, default `"0"`, shared by Dispatch and Ghost,
  valid when `Number.isSafeInteger(Number(text)) && >= 0` (so `""`, `" "`, `"1e2"`, `"0x10"` are valid and
  `"-1"`, `"1.5"`, `2^53+1`, `"abc"`, `"Infinity"` are not); Schedule / Share / Truck-schedule predicates,
  the `Sharing…` label, busy keys and message ownership; schedule success clears the matching selection,
  reloads the jobs and opens Scheduled Plunder, failure keeps selection and tab; share prunes only shared
  uuids; the action message is cleared by every tab change.
* **Fence**: those controls enable only when the provider implements the operation. The production map
  API implements none, so production keeps them disabled (`data-runtime-fenced`). Original UI predicates
  (e.g. share requires `online`, schedule does not) are computed separately from this fence.

Documented differences, deliberately not reproduced: (1) the original issues an extra search after every
options reply because its name-selection object is always replaced, and refreshes rows once a second while
scanning and at scan completion — canonical options/scan polling differs and is outside this scope;
(2) the original only logs a search failure, the canonical page keeps its accepted visible error banner;
(3) with no data server the original still sends `serverId:0`, the canonical page disables Search;
(4) share/clear translation was corrected in final commit 5b76ae8 and is no longer an open difference;
(5) alliance / level / treasure-type validation after an options refresh is not implemented (filter
scope, accepted behavior left unchanged).

## C — Scheduled Plunder presentation

`ScheduledPlunder.jsx`, `mapPlunderPresentation.js`, `mapPlunderFixtures.js` (new, written by a subagent
and reviewed/integrated by the worker). The tab renders the three recovered groups — Dispatch, Ghost Scout,
Truck — from `ot` (20712) and `st` (24391): exact columns and widths, empty row, reward cells, date cells,
quality/special, the secret status chain, the truck result chain with its countdown, error-text
resolution (`b`/`we`, `Te`/`C`), Cancel / Plunder Again / Clear predicates, tab and result counts
(`dispatch + ghost + truck` rows), job loading on mount, change events and every tab entry, and a page
clock. No state enum, dialog or skeleton was added; unknown values follow the source expressions.

* Differential: the ACTUAL original `ot`/`st` (and helpers from the asset) vs the canonical components,
  react-dom/server, 9 locales, 10,482 renders including a time sweep at/around 106 boundaries, 4,508 + 3,008
  generated status/error/timing combinations, 225 button/handler checks (`scheduled/results.json`).
  `actionsEnabled=true` markup is byte-identical to the original apart from the disclosed reward-icon
  attribute; 51/51 mutations detected; 61 fixture branches covered; the pre-campaign surface (single empty
  row, hardcoded `0`) fails parity in all fixture states (`baselineFailsParity`).
* Runtime: the production map API has no job provider, so production shows zero jobs and every job action is
  fenced. The job row schema of the native list is UNKNOWN (fixtures use only fields the frontend reads);
  native cancel/clear/schedule remain BLOCKED/out of scope.
* Fixtures (`map-scheduled`, `map-scheduled-populated`, `map-scheduled-conditional`, `map-actions-*`) are
  synthetic and explicit; the preview provider stays `online:false`; the presentation-only `online` input is
  disclosed; scheduling/cancel/clear/share methods exist only in those dedicated states and all reject with
  `PREVIEW_NATIVE_ACTION_BLOCKED`. A row showing Victory/Succeeded renders one recovered branch, it is not a
  game result.

## D — integration and verification

* `check-integration.mjs` (production callbacks/effects, deferred replies, controlled clock): cache
  restoration with typed keyword and name selection, selection with cache/refresh/search, rapid navigation
  with deferred replies, Normal<->Scheduled transitions and counts, page-clock boundaries (999 ms no change,
  1000 ms advance), schedule/share/cancel/clear/plunder-again flows, fences, clear-data, stale-name drop —
  14/14 pass; the baseline fails 11.
* Regression replays through `replay-historical.mjs` (new adapter; shim defines the new free identifiers and
  widens one effect locator; historical scripts/results untouched): FILTERS 472 queries (baseline 330
  mismatches preserved), table regression, Treasure Checking 216/384/8, row actions 160, review-states.
  The TRANSPORT and LEAD-TABLES checkers already failed at `3e8617c` (`setItemKeyByKind`, recorded in
  NAVIGATION-001 README); LEAD-TABLES is still covered through the table-regression replay.
* Browser (offline preview, worker-run, `browser-results.json`): provider marker `preview`, server 321,
  populated rows verified before claiming; real keyboard/pointer input for typing vs Search, name typing, row
  selection, schedule (fenced, nothing created) and Clear history (blocked message, rows unchanged);
  DOM-dispatched input disclosed for tab/selection sweeps; English/Japanese, light/dark, 375 px with no
  horizontal page overflow; fresh-load console errors 0 (one HMR-only warning during development recorded).
* `npm.cmd run check`, `npm.cmd run build`, `npm.cmd run check:production-build` pass (fingerprints are in
  the evidence README); `validate-evidence.mjs` passes; protected WIP unchanged and unstaged.

## Remaining gaps (exact)

Original pixel/visual parity; native job producer, row schema and native scheduling/cancel/clear/share/
claims; Treasure native wiring; scan header timing display, scan/options polling parity and the extra
post-options search; alliance/level/treasure-type refresh validation; the narrow
viewport keyword-input height (existing CSS, unchanged). Nothing here establishes full Map, overall UI,
original pixel, native persistence or gameplay parity.

## Late independent review (original/final-review.md)

A second executed comparison of the committed page found 8 deviations. Fixed in this delivery: server change at
page > 1 sent a stale-page first request (now page 1, matches the original); share/clear failure messages now use
the original error translator (`common.actionFailed` fallback). NOT fixed, left as exact remaining gaps (assignment
excludes them or says preserve accepted behavior): alliance / dispatch-level / treasure-type validation after options
refresh and filter resets on clear-data (`C02`, `C03`); export success/failure messages and scan-start banner
translation (`C06`, `C27`); row refresh while scanning and at scan completion (`C07`); Treasure lucky-default and
persistence plus always-sent boolean treasure query fields (`C08`, `C28`); alliance named `none`/`all` collision
(`C09`); player-mark button also disabled offline/scanning/busy in the canonical page (`C15`, native availability fence).
Note: after the translator change a rejected preview action shows the localized `common.actionFailed` text instead of
the raw `PREVIEW_NATIVE_ACTION_BLOCKED` text recorded in browser-results.json BR6 (that browser observation predates
the change; the synthetic integration test covers the new text). PM-026 now independently
rechecks the final English/Japanese browser message and unchanged row counts under
`evidence/lwbridge-0.3.17/ui/LWB317-PM-026/browser-recheck.json`. The historical BR6
record is preserved and superseded, rather than rewritten as a final-state observation.
