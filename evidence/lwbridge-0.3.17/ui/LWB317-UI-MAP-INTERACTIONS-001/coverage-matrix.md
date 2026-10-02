# LWB317-UI-MAP-INTERACTIONS-001 coverage matrix

Original = `MapDataPanel-B4GXEND2.js` (SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`);
locators are UTF-8 byte ranges in the original asset (full list: `original/contract.md`, 88 items).

Proof legend (kept separate on purpose; none is original-pixel or native evidence):

| Code | Meaning |
|---|---|
| **O** | Differential against the ACTUAL original component executed from the asset bytes (`original/original-runtime.mjs`), same user-level scenario through the shared driver: `check-interactions.mjs` (scenario id given). Synthetic local replies. |
| **S** | Synthetic production-callback/effect test (`check-request-lifetime.mjs`, `check-integration.mjs`, `check-navigation-replay.mjs`) with deferred replies and a controlled clock; not compared with the original. |
| **R** | Rendered-markup differential of the ACTUAL original `ot`/`st` tables against the canonical components (`scheduled/check-scheduled-plunder.mjs`, react-dom/server, 10,482 renders). |
| **M** | Negative control: a deliberate defect in a copy of the canonical code is detected (`check-mutations.mjs` 14/14; scheduled 51/51). |
| **B** | Browser observation in the in-app browser against the offline preview (`browser-results.json`). `real` = browser pointer/keyboard input; `dom` = DOM-dispatched or `form_input`. |
| **H** | Historical regression replay through the new adapter (`replay-historical.mjs`). |

Status vocabulary follows `AGENTS.md` section 4. `IMPLEMENTED_NOT_VALIDATED` = implemented and source/local-proven, no pixel, native or live proof.

## A. Request lifetime (Milestone A)

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| A1 | Pending success/rejection after backend loss cannot change rows/total/error/loading | original has no cancel flag; request generation `rr` 38513-39238, tab `ir` 39238-39496 | `MapDataPage.jsx` search effect cleanup `searchGeneration.current += 1` | S `check-request-lifetime` (backend-loss x2): baseline 0 failures, 305240e 2 failures, current 0 | IMPLEMENTED_NOT_VALIDATED |
| A2 | Unmount, provider replacement (success/rejection), re-entry, stale rejection/finally, effect re-run, server transition, Scheduled entry | same | same | S 13 scenarios x 3 sources = 38 runs; 305240e fails the 4 disposal scenarios (backend loss x2, unmount x2) | IMPLEMENTED_NOT_VALIDATED |
| A3 | Tab-cache correction of NAVIGATION-001 preserved | `ye` 554, server effect 33502, tab effect 34677 | unchanged | S `check-navigation-replay` (historical `campaign()` text; baseline reproduces the 6 recorded defects; current 0) | IMPLEMENTED_NOT_VALIDATED |
| A4 | Lead reproduction `PM-025/check-availability.mjs` | - | - | S now reports 0 current failures; recorded failing results untouched | - |

## B. Keyword, search and loading

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| B1 | Typing never searches; no keyword dependency; no timer/debounce/throttle | deps 34816-34869; input 50930-51100; timers `kw-timers` (6 sites, none in search path) | `changeKeyword`; keyword absent from search-effect deps | O `keyword.typing-does-not-search`, `keyword.no-timer-or-debounce-after-time-passes`; M; B real (52 items unchanged after 2.5 s) | IMPLEMENTED_NOT_VALIDATED |
| B2 | Search click on page 1 searches directly; on page > 1 only resets page, one request | onClick 54355-54377 | `submitSearch` | O `keyword.search-click-on-page-1...`, `...page-2-goes-to-page-1...`; M; B real (typed text applied: 1 item) | IMPLEMENTED_NOT_VALIDATED |
| B3 | Typed-but-unsubmitted text is used by every effect re-run (filter, tab, page) | `nr` keyword 37677-37687 | effect closure reads `keyword` | O `keyword.typed-but-unsubmitted-text-is-used-by-a-filter-change`, `...tab-change-keeps-input...`, `...page-change-queries-with-typed-text` | IMPLEMENTED_NOT_VALIDATED |
| B4 | Keyword untrimmed, shared by all tabs, kept across tab change | `kw-state` 30434-30460 | unchanged raw string | O (same scenarios) | IMPLEMENTED_NOT_VALIDATED |
| B5 | Search button never disabled by loading; click while loading starts a newer request and retires the older | 54327-54407 | disabled = `!backendAvailable \|\| !dataServerId` only | O `search.button-is-not-disabled-by-loading`, `search.click-while-loading...`; M | IMPLEMENTED_NOT_VALIDATED |
| B6 | Connection (`online`) change refreshes the list | deps `l` | `online` added to deps | O `search.connection-change-refreshes`; M | IMPLEMENTED_NOT_VALIDATED |
| B7 | Search failure clears rows and total | `rr` catch | unchanged | O `search.failure-clears-rows-and-total` | IMPLEMENTED_NOT_VALIDATED |
| B8 | Original logs the failure only; canonical keeps its accepted visible error banner | `rr` catch (log) | `queryError` banner | DOCUMENTED difference (`original/canonical-differential.json` `search-error-visible`) | DOCUMENTED_DIFFERENCE |
| B9 | With no data server the original still issues a request (`serverId:0`); canonical disables Search and sends nothing | 54327-54407 / effect 34677 | runtime availability fence | DOCUMENTED difference | DOCUMENTED_DIFFERENCE |
| B10 | Extra search after every options reply (name-selection object identity), 1 s row refresh while scanning, scan-completion refresh | `name-refresh-validation` 35316-35466, ticker/refresh 34488-34567 | not reproduced (canonical polls options every 5 s; scan polling out of scope) | DOCUMENTED difference | NOT_IMPLEMENTED (out of assigned scope) |

## C. Resource / Monster name selection

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| C1 | Selecting a name empties the keyword, resets page, one search | `name-onchange` 51184-51243 | `selectName` | O `name.select-clears-keyword-resets-page-and-searches-once`; M | IMPLEMENTED_NOT_VALIDATED |
| C2 | Typing while a name is selected drops that tab's name (first keystroke searches); typing without a name does not | `kw-onchange` 50967-51031 | `changeKeyword` | O `name.typing-after-selecting...`, `name.typing-without-a-selected-name...`; M; B real+dom | IMPLEMENTED_NOT_VALIDATED |
| C3 | Resource and Monster selections are independent per tab and survive tab changes | `name-state` 30828-30854 | two states | O `name.resource-and-monster-selections-are-independent` | IMPLEMENTED_NOT_VALIDATED |
| C4 | Option text `gameText(key) \|\| key` + ` (count)`, select label `common.name` | `name-option-text` 51312-51412, `name-j` 7467-7558 | `lookupMapText` | O `name.option-text-uses-game-text-then-key`; M; B (labels visible) | IMPLEMENTED_NOT_VALIDATED |
| C5 | A selected name no longer offered is cleared when options refresh | `name-refresh-validation` 35316-35466 | `loadOptions` | S `check-integration` options-refresh scenario (canonical only; the original also refires a search, B10) | IMPLEMENTED_NOT_VALIDATED |
| C6 | Alliance / dispatch level / treasure-type validation after options refresh | same expression | not implemented (filter scope, accepted behavior unchanged) | - | NOT_IMPLEMENTED |

## D. Selection ownership and lifecycle

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| D1 | Dispatch/Ghost share one map `{serverId:uuid -> row + taskKind}`; Truck has its own map; payload is the row snapshot | `sel-dispatch-state` 31301-31326, `sel-truck-state` 31327-31353, toggles 39593-39866 | `dispatchSelection`/`truckSelection`, `mapInteractions.js` | O `selection.dispatch-toggle-membership-and-count`; B real | IMPLEMENTED_NOT_VALIDATED |
| D2 | Stored key untrimmed, membership lookup trimmed (padded uuid never shows checked); duplicate and empty ids share a key; same uuid on two servers = two entries | `sel-checkbox` 17072-17832 | `selectionStorageKey`/`selectionMembershipKey` | O `selection.payload-keys-and-identifier-edge-cases`; M | IMPLEMENTED_NOT_VALIDATED |
| D3 | Dispatch/Ghost selection emptied on EVERY tab change (also Dispatch<->Ghost) | `tab-effect-change` 34121-34182 | `[tab]` effect | O `selection.dispatch-resets-on-any-tab-change...`; M x2; B dom | IMPLEMENTED_NOT_VALIDATED |
| D4 | Truck selection persists across tab, page, filter, search, refresh and server change | `sel-truck-state` | not reset anywhere except clear-data/success | O `selection.truck-is-independent-and-persists...`; M; B dom | IMPLEMENTED_NOT_VALIDATED |
| D5 | Dispatch selection survives page, filter/sort/search, refresh and server change | `server-effect` 33502-33682 | server effect no longer resets selection | O `selection.dispatch-survives-page-filter-sort-search-and-server-change`; M; S refresh | IMPLEMENTED_NOT_VALIDATED |
| D6 | Clear-data empties both selections | `tr-clear-data` 37087-37497 | `clearData` | S `check-integration` clear-data scenario (not in the original-runtime differential) | IMPLEMENTED_NOT_VALIDATED |
| D7 | Counts = number of stored keys (not on-screen rows) in labels and predicates | `sel-derived-counts` | `selectionCount` | O (button labels) | IMPLEMENTED_NOT_VALIDATED |
| D8 | Row checkbox disabled predicates and rendering | `sel-checkbox` | `mapTaskSelectable` (accepted LEAD-TABLES) | H `check-table-regression`, `check-review-map-states`, `check-row-actions` | IMPLEMENTED_NOT_VALIDATED |

## E. Random delay, action predicates and flows

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| E1 | Delay is one raw string, default `"0"`, shared by Dispatch and Ghost, not reset by tabs | `delay-state` 31434-31461, `delay-input` 55299-55431 | `randomDelay` | O `delay.input-default-and-controlled-value`; B (value `0`) | IMPLEMENTED_NOT_VALIDATED |
| E2 | Validity `Number.isSafeInteger(Number(text)) && >= 0` (`""`, `" "`, `"1e2"`, `"0x10"` valid; `-1`, `1.5`, `2^53+1`, `abc`, `Infinity` invalid) | `delay-valid-Kn` 32925-32959 | `parseRandomDelay` | O `delay.schedule-button-predicate-per-delay-text` (12 strings); M | IMPLEMENTED_NOT_VALIDATED |
| E3 | Schedule-selected / Share / Truck-schedule predicates and labels (Share flips to "Sharing") | `btn-schedule-dispatch` 55466-55656, `btn-share-alliance` 55673-55867, `btn-schedule-truck` 55881-56076 | `mapInteractions.js` | O `actions.*`, `flow.share-label-selection-pruning...`; B (`Sharing…` disabled) | IMPLEMENTED_NOT_VALIDATED |
| E4 | Canonical runtime availability fence: no provider method => button disabled, `data-runtime-fenced`; production map API has none | - (canonical only) | `providerSupports` | S `fence: without a provider ...` | IMPLEMENTED_NOT_VALIDATED |
| E5 | Schedule success: busy `schedule`, selection cleared, job list reloaded, navigate to Scheduled Plunder; failure keeps selection and tab | `delay-schedule-handler` 40873-41134, `fn-ur` 40620-40873 | `scheduleSelectedDispatch/Trucks` | O `flow.schedule-dispatch-success...`, `flow.schedule-dispatch-failure...`, `flow.schedule-truck-success...`, `flow.schedule-with-invalid-delay...`; S | IMPLEMENTED_NOT_VALIDATED (provider is a harness stub) |
| E6 | Share: only shared uuids leave the selection; success/partial messages; label restored | `fn-pr` 41655-42146 | `shareSelectedDispatch` | O `flow.share-*`; S | IMPLEMENTED_NOT_VALIDATED |
| E7 | Action message is cleared by every tab change | `tab-ir` 39238-39496 | `changeTab` | O `flow.action-message-is-cleared-by-a-tab-change`; M | IMPLEMENTED_NOT_VALIDATED |
| E8 | Share error text is the original's translated `m(t, error)`; canonical shows the raw `code: message` | `fn-pr` | `errorText` | DOCUMENTED difference (translator `m` not recovered into the canonical page) | PARTIAL |
| E9 | Treasure claim buttons, claim result, polling | `btn-claim-*` | untouched (still disabled) | - | OUT_OF_SCOPE (native Treasure wiring) |

## F. Scheduled Plunder presentation

| # | Behavior | Original locator | Canonical | Proof | Status |
|---|---|---|---|---|---|
| F1 | Three groups in order Dispatch, Ghost Scout, Truck; Dispatch/Ghost split by `taskKind` | 57146 (filters), `ot` 20712, `st` 24391 | `ScheduledPlunder.jsx` | R group filters/count; B | IMPLEMENTED_NOT_VALIDATED |
| F2 | Columns, widths, headers, empty row, reward cell, date cells, quality/special | `ot`/`st` | `SecretPlunderGroup`, `TruckPlunderGroup` | R (9 locales, 594 fixture renders, 4,508+3,008 matrix renders); M 51/51 | IMPLEMENTED_NOT_VALIDATED |
| F3 | Secret status chain (succeeded/failed/cancelled/expired/running/waiting/ready/protected/pending), Truck result chain incl. countdown | `ot` inner `f`, `st` inner chain | `mapPlunderPresentation.js` | R time sweeps at/around every boundary (106 boundaries) | IMPLEMENTED_NOT_VALIDATED |
| F4 | Error text resolution (E000000, game text, DISPATCH_PLUNDER_* table, numeric rejection, unknown; truck table) | `b`/`we` 996-2192, `Te`/`C` 2671-3363 | translators | R 432 + 324 cases | IMPLEMENTED_NOT_VALIDATED |
| F5 | Conditional actions: Cancel only scheduled/waiting; Plunder Again online+succeeded+not invalid/full/expired+no in-flight twin; Clear disabled while busy or nothing finished | `ot`/`st` | predicates | R 225 interaction buttons; O (flow Cancel/Again/Clear call identities) | IMPLEMENTED_NOT_VALIDATED |
| F6 | Tab count = dispatch+ghost+truck job rows; also the Scheduled result count | tab count 50824, result 56491 | `scheduledCount` | R count; O `flow.scheduled-tab-lists-counts...`; B (30) | IMPLEMENTED_NOT_VALIDATED |
| F7 | Jobs load on mount, on provider change events and on every Scheduled entry; 1 s page clock | `fn Q`, 34121-34182 | `loadPlunderJobs`, page clock | O same scenario; S clock (999 ms no change, 1000 ms advances) | IMPLEMENTED_NOT_VALIDATED |
| F8 | Cancel identities (`ghost:` prefix), busy keys `serverId:uuid`/`truck:...`/`clear:kind`, reload after, failure silent | `fn-hr/gr/_r/mr` | handlers | O `flow.scheduled-cancel-identities...`, `flow.scheduled-plunder-again-and-clear-history` | IMPLEMENTED_NOT_VALIDATED |
| F9 | Runtime job producer and native cancel/clear/schedule | `map_plunder_jobs_list` etc. named in index asset | no production provider methods | fence S + browser blocked-clear message (B real) | UNKNOWN/BLOCKED (native out of scope) |
| F10 | Explicit offline fixtures `map-scheduled`, `-populated`, `-conditional` (+ `map-actions-*`), `online:false` provider, presentation `online` disclosed | - | `mapPlunderFixtures.js`, `mapPreviewApi.js` | R 61 fixture branches; B | IMPLEMENTED_NOT_VALIDATED |
| F11 | Job row schema from the native producer | frontend reads only the fields used | fixtures use only those | - | UNKNOWN |

## G. Integration (Milestone D)

| # | Behavior | Proof | Status |
|---|---|---|---|
| G1 | Cached page restoration keeps typed keyword and refreshes with it; name selection survives cache restoration | S `check-integration` (14 scenarios; baseline fails 11) | IMPLEMENTED_NOT_VALIDATED |
| G2 | Selection + cache: Dispatch selection reset on tab change while page/rows restored; selection retained across refresh/search | S | IMPLEMENTED_NOT_VALIDATED |
| G3 | Rapid navigation with deferred replies settles only on the final tab incl. Scheduled boundary | S; H navigation replay | IMPLEMENTED_NOT_VALIDATED |
| G4 | Normal <-> Scheduled transitions and counts | S; O; B | IMPLEMENTED_NOT_VALIDATED |
| G5 | English/Japanese, light/dark, 375 px viewport (no horizontal page overflow) for Scheduled and Secret Task action surfaces | B screenshots (`screenshots/`) | IMPLEMENTED_NOT_VALIDATED |
| G6 | Accepted regressions: filters 472 queries, Checking 216/384/8, row actions 160, table regression, review-states | H | PASS |
| G7 | Console errors on a fresh load and a 10-tab cycle | B (0); one HMR-only React warning during development recorded as artifact | PASS |

Not claimed: full Map parity, overall UI parity, original pixel parity, native persistence/scheduling, gameplay, Treasure claims, scan header timing display, scan/options polling parity.
