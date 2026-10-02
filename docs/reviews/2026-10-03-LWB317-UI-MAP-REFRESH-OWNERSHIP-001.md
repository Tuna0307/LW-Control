# LWB317-UI-MAP-REFRESH-OWNERSHIP-001 — delivery review

**Date:** 2026-10-03
**Status:** AWAITING_REVIEW
**Branch:** `research/offline-controller`
**Parent campaign:** `LWB317-UI-MAP-CLOSEOUT-002` remains PARTIAL; Auto implementation is separate.

## Result

The interrupted summary/options ownership unit is complete for the assigned
source/local frontend scope. The existing unfinished production integration was
retained and narrowed to the exact recovered 0.3.17 lifecycle: `App.jsx` owns Map
summary bootstrap, scan acknowledgement, completion refresh and the guarded
five-second connected poll; `MapDataPage.jsx` owns data options by data server and
options revision. A summary-only parent poll cannot reload options.

One source-backed defect was found by the new executable lifecycle proof. When a
controlled parent cleared its summary during owner/profile replacement, the panel
kept previous option counts marked ready. The recovered parent-summary effect
clears counts/readiness when there is no matching summary. The production effect
now does the same only for the controlled parent-managed path, preserving the
accepted legacy/uncontrolled server-loss behavior.

## Source authority

Reference executable SHA-256 is
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Exact frontend assets rechecked as:

- `MapDataPanel-B4GXEND2.js` — `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`
- `index-BVfnK1wp.js` — `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`

`source-locators.json` pins seven UTF-8 anchors: completion rows/options,
progress rows timer, options generation/count acknowledgement/dependency boundary,
parent summary producer and connected five-second poll.

## Distinguishing execution proof

`milestone-b/check-refresh-ownership.mjs` now compiles and executes the actual
production `App.jsx` callback/effect body with inert bridge responses and a clock
that runs intervals and timeouts. It also executes actual current
`MapDataPage.jsx` in the controlled boundary. The immutable `123459d` baseline is
kept as the ownership counterexample: one child summary call, one child scan
listener, and options nested under the child summary callback.

Current results distinguish that baseline:

- App mount creates the selected-profile bootstrap and connected immediate poll;
  an unresolved poll suppresses the next five-second overlap.
- A newer summary owner wins over late bootstrap work. Offline stops new polls;
  reconnect starts one immediate poll. Same-profile work already in flight may
  settle after connected-effect cleanup, matching the exact original producer.
- Reading-to-stopped acknowledgement creates one completion summary request.
  Profile replacement fences old-profile and older same-profile replies.
- Deferred rejection after poll cleanup preserves current state; deferred success
  after App unmount cannot replace it. Status and scan listener installs/disposals
  balance 2/2 each across the profile replacement.
- The controlled panel makes zero summary calls and installs zero scan listeners.
  Options are `[321,321]` for bootstrap plus completion; a five-second parent
  summary cadence does not reload them.
- Scan progress advances rows after the recovered one-second trailing timeout and
  leaves options unchanged. Mount-already-reading followed by completion advances
  options once. Completion-driven refreshed options clear a selected Resource
  name that is no longer offered.
- Parent count acknowledgement updates matching parent summary counts, while a
  cleared/mismatched controlled summary resets panel counts/readiness.
- Provider replacement fences an obsolete options reply.

Independent/current lifecycle replays retain the established edge traces:
PM-027 options redirect is `[321,322,321]`; focused delayed Clear is `[322,321]`.

## Regression and package validation

Passing checks used for this delivery:

```text
LWB317_MAP_CLOSEOUT_REFRESH_OWNERSHIP_OK parent=2 childSummary=0 childListeners=0 options=321,321
LWB317_MAP_CLOSEOUT_DELAYED_CLEAR_OK options=322,321
LWB317_MAP_SCAN_HEADER_OK comparisons=270 baselineMismatches=29 clock=6 unmount=PASS
LWB317_UI_MAP_FILTER_LIFECYCLE_R1 cases=5 failures=0
LWB317_PM027_INDEPENDENT cases=6 failures=0
LWB317_MAP_REFRESH_FEEDBACK_OK export=45 scan=19 refresh=11 baselineDefects=15
LWB317_REQUEST_LIFETIME_OK baselineFailures=0 deliveryFailures=4 currentFailures=0 scenarios=38
LWB317_INTERACTIONS PASS scenarios=38 currentMismatches=0 baselineMismatches=31 originalErrors=0
LWB317_MAP_REFRESH_OWNERSHIP_EVIDENCE_OK locators=7 browser=2 protected=6
```

`npm.cmd run check`, `npm.cmd run build` and
`npm.cmd run check:production-build` pass from `src/LWBridge.UI-0.3.17`. The built
production package reports hashes
`5d8d24301b1845f22cdda69ede0c0cd06560bc351e597a48a184213f977c4a78`
and `91345a1b8793ff573b78ba7898265c040817d302548382dc5c6542a5360dde3e`.
Vite emits only its existing >500 kB chunk-size advisory.

Historical validators were not rewritten to manufacture green output. The broad
FILTER-LIFECYCLE checker expects the pre-recovery Treasure native `<select>` and
cannot reach its later Clear case; the focused current Clear replay covers that
boundary. The standalone NAVIGATION-001 extractor predates later component
constants (for example `DEFAULT_RANDOM_DELAY_TEXT`); the maintained current
interactions suite executes the server-change/page-3 navigation boundary. One
historical integration scenario assumes options reload after five seconds; exact
0.3.17 instead keys options to `[dataServer, optionsRevision]`. The new checker
keeps its useful Resource-name invalidation assertion on the source-backed
completion revision.

## Browser evidence

Bounded offline browser QA used `map-city` at server 321. The connected browser
DOM and saved settled Edge captures were inspected for:

- English/light: `World Map Data`, counts `52,53,54,55,56,57,58,59,0`, 50 City
  rows, zero console warnings/errors.
- Japanese/dark: `世界地図データ`, the same counts and 50 City rows, zero console
  warnings/errors.

Saved screenshots and their hashes are in `browser-smoke-results.json`. The
preview fixture's disconnected-status sentence remains English in the Japanese
state; that pre-existing fixture content is outside this ownership unit. No
native operation, scan, gameplay action or original-pixel comparison was run.

## Preservation and remaining limits

`validate-evidence.mjs` rechecks generated JSON, current App/Map source hashes,
the seven exact source locators, both screenshot hashes and all six protected WIP
hashes. Protected `previewAfkFixtures.js`, `.scratch-lwb317/`, CORRECT-003
screenshots, historical FILTER-LIFECYCLE result and already-dirty R1 result stay
unstaged. Agent-a/agent-b packets are unchanged.

This delivery proves only summary/options refresh ownership and affected local UI
regressions. Auto Scan implementation remains deferred. Native providers/live
runtime, gameplay, full UI/original pixels and global acceptance remain outside
scope. Project lead decides acceptance.
