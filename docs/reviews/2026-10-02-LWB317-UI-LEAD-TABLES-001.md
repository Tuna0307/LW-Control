# LWB317-UI-LEAD-TABLES-001 — Map table presentation and lead takeover

Date: 2026-10-02. State: **AWAITING_REVIEW**. Owner: project lead; owner requested
a larger implementation block while the worker rests. Baseline:
`4e29806db72c83e37c0ba96f82270c66b5a4cbba`.

The eight normal Map tables now consume recovered column definitions and value
formatters. This fixes missing Secret Task/Ghost Ops statuses, raw Treasure state
strings, missing charging percentages, incorrect truck loot caps, quality and
game-text fallback formatting, column widths/classes and conditional item sorting.
It completes this source/local correction scope; it does not establish full Map
UI, original pixel or live-function parity.

## Reference and exact locators

- Target executable: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`;
  SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`;
  SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
  Column factory `nt` at UTF-8 byte **9594**; reward renderer `fe` at **16260**;
  one-second clock effect at **33911**. Exact helpers, type constants, selection
  `me`, task renderer `pe`, width expression and item-filter callback are pinned
  with complete expressions and UTF-8 locators in `map-table-results.json`.
- `index-BVfnK1wp.js` in that directory;
  SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
  Truck cap/state functions `Fe` / `Ie` are executed from their exact declarations;
  sort-arrow paths are pinned in `manifest.json`.
- `rewardDisplay-eZWrd6iS.js` function `e`: exact compact-count declaration/hash/byte
  locator in `map-table-results.json`; it is executed in the differential harness.
- Current-client occupancy producer: `src/LWBridge.Desktop/FirstLiveResultImporter.cs`
  lines 158–165. `rebuildGatherOccupancyKnown` and `rebuildGatherOccupied` are existing
  adapter fields, not original frontend fields. The producer's unchanged hash and
  exact assignment are pinned in `manifest.json`.

`EXACT_BYTES` describes these recovered source facts. The correction is
`IMPLEMENTED_NOT_VALIDATED` at the overall product level, with the stated source/
local checks passing and independent acceptance pending. No new `LIVE_PROVEN`
claim is made.

## Changes and distinguishing behavior

`mapTablePresentation.js` is the canonical recovered presentation module; the
original column factory's bindings are expanded and reused by `MapDataPage.jsx`.
Original CSS/assets and native bridge/commands remain unchanged.

- Secret Task and Ghost Ops derive expired/full/pending/protected/ready from
  expiry/completion/plunder timestamps and counts, using the recovered translated
  labels. Their checkboxes and Truck checkboxes use recovered eligibility rules.
  A render-only one-second clock updates these table states/countdowns and cleans
  up when leaving the tab. Scheduling and scanning clocks remain separate.
- Truck cap uses `maxLootCount`, with the original special-UR cap of one, rather
  than guessing from `maxRobTimes` or forcing a cap equal to the robbed count.
- Treasure uses the recovered type/supplies/name resolver, charging percentage,
  world state and player-state/claim-reason precedence. Claimed takes precedence
  over foreign-alliance/no-scout reasons. Unknown enums keep original unknown copy.
- Quality values at or above five render UR. City expired shields render `-`;
  timestamps accept the source seconds/milliseconds shape. Supplied game text is
  used only when the lookup returns a meaningful value; otherwise the recovered
  translated fallback is used. No native game-text provider was invented.
- Resource occupancy known/occupied fields retain the current-client adapter;
  unknown is displayed as `—`, rather than being labelled Idle. Rows without that
  adapter use the original trimmed nonzero gather-UID/march test.
- All eight tables use recovered column widths plus the exact eight-pixel
  adjustment, classes and table labels. Train and Truck use Live Target;
  Secret Task/Ghost Ops use Coordinates, as the original `O` constant specifies.
- Rewards use source ordering, selected-item-first behavior, compact counts,
  localized full-count title/aria descriptions and recovered container classes.
  Missing native images retain disclosed placeholders.
- Item sorting exists only with a selected item filter. Clearing that filter
  removes `itemCount` from the active table's sort list and resets pagination,
  matching the actual original callback. Sort arrows and priority descriptions
  use recovered paths/copy.

`map-table-states` is an explicit browser-preview-only dataset. Its numerical task
UUIDs and time/state combinations allow visible positive/negative presentation
checks. It remains `online:false`, cannot be selected in native/native-unavailable
modes, and all mutation methods reject. Its query clock uses the same captured
time as its synthetic rows. Existing fixed dataset/query semantics remain checked.

## Verification

`check-map-tables.mjs --verify-record` executes recovered original declarations
against imported production helpers and actual extracted production JSX/callbacks:

- 288 column metadata cases, 15,264 value comparisons across eight kinds/nine locales;
- 243 selection and 81 task-label comparisons;
- 3,150 Treasure type/reason cases; eight actual item-filter callback cases;
- 72 actual table render cases, including original reward-renderer/width comparisons;
- 27 clock cleanup cases and 11 compact-count boundary cases;
- existing fixture pagination, zero matches, alliance/level/sort queries, seven
  rejected native actions and both native-mode fences; variant pending/completed
  queries distinguish one pending from 56 completed synthetic tasks.

Twelve baseline value differences are preserved in the results, including examples
that distinguish the former missing/raw values from recovered output.

Browser evidence covers all eight tables, widths/headers/disabled native actions,
Treasure charging/claimed/foreign states, Truck protected/full states, five Secret
Task statuses, unknown Resource occupancy, item sort selection/clear and Japanese
dark Treasure. All three JPEG captures were visually inspected alongside DOM
assertions; an initially premature Task capture was replaced after checking its
actual table and five states. `manifest.json` pins the corrected format/hash. Browser console
errors captured: zero.

Accepted Home translation/root acknowledgement/switch checks pass. Pending Home
busy checks pass (13,824 renders, 384 predicates, 27 preference checks, 63 fixtures);
pending Trade 003E passes 11 presentation states and four missing-status cases.
Fresh author browser rechecks cover five Home busy states, retained Trade loading,
retained failure and Purchased history. These are **author rechecks**, not an
independent review or acceptance of Home busy/Trade 003E.

Trade selection/history/cross-server regressions, `npm.cmd run check`, build and
`check:production-build` pass. All nine catalogs remain at 1,383 messages; the
static checker now inventories literal keys in the new recovered module.
Source/package fingerprints: `1280d8a4df7aec2f81260a8081c0dda7626bad06e8771d25cea4e3ae0c49daf2`
/ `b2a903176188b57cd02f281ffaded42aa05a0d6503595e25ee3e5fcfa09127ae`.
Vite retains its advisory chunk-size warning. New evidence parsing/hashes and
protected-WIP checks pass; diff checks pass.

Historical CORRECT-003 `check-state.mjs` still expects boolean `true` for Trade
fetch failure at line 86. Current 003E supplies the recovered error string. The
string already exists at this task's baseline; this historical snapshot assertion
fails and is recorded in `verification.json`, not rewritten or reported passing.
Current Trade error presentation and Map query/fencing checks pass separately.

## Remaining gaps and continuation

See `evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/coverage-matrix.md`.
Native game texts/images, action-button/follow/mark visuals, original runtime pixels,
complete per-tab filter state, Scheduled Plunder UI and native scheduling/claim
providers remain separate gaps. Existing Map live-state blockers remain unchanged.

Returning worker: finish assigned independent `LWB317-REVIEW-HOME-BUSY-001`, then
independently review this Map checkpoint in a separate bounded work item. Trade
003E also remains awaiting independent review. Do not resume parent CORRECT-003
or a gameplay/backend campaign automatically.

Protected `previewAfkFixtures.js`, `.scratch-lwb317/` and parent CORRECT-003 screenshots
retain their initial hashes and remain unstaged. Owned preview tab/listener cleanup
and Git delivery are recorded in `delivery.json`; no other chat was restarted.
