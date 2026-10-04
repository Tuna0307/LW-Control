# LWB317-UI-MAP-TOOLBAR-VISUAL-001

Status: AWAITING_REVIEW, worker delivery complete 2026-10-04; project-lead acceptance pending. Size: upper-medium, three sequential milestones.
Product delivery baseline: `5a62c25118e66ec3203316bca7c82fbf43d9c705`.

## Goal and startup

Close the recovered-source/local visual gate for Map data tabs, search/filter
toolbars, action/count feedback and pagination. Work across the eight normal Map
data kinds plus the Scheduled Plunder navigation/count toolbar. Compare and fix
source-proven local presentation mismatches; deliver executable evidence, not a
code-reading-only report. This is phase 2 UI work, not native function work.

Work in `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`, on
`research/offline-controller`, alone with no subagents or delegation. Read
`AGENTS.md`, root `task.md`, `docs/AI_WORK_PROTOCOL.md`, `docs/lwbridge-ui.md`,
`docs/UI_FINISH_CHECKLIST.md` and this assignment. Inspect starting HEAD/status;
do not reset a newer lead documentation checkpoint to the delivery baseline.
Preserve every starting modified/untracked path and historical evidence. Run
the ten-file guard in HOME-PREFERENCE-LIFETIME-001B without `--record`.

Reference EXE: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Original assets are in `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`:
`MapDataPanel-B4GXEND2.js`, `index-BVfnK1wp.js`, `index-rIL9Fpht.css`.
Verify hashes and recover exact UTF-8 slices for each assigned renderer/callback.

## Scope and intentional behavior

Owned production files: only source-proven presentation and dropdown-local UI
behavior in `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`,
`MapTreasureTypeFilter.jsx`, and `MapRetainedGoodsFilter.jsx`.
The assigned MapDataPage regions are `.map-search` (tab list and toolbars),
query-error presentation and `Pagination`; do not modify scan headers/cards,
table cells/rows, ScheduledPlunder job tables or unrelated page components.

Inspect dependencies as needed, but do not change App, providers, backend/query
schemas, shared CSS, locale catalogs, helper contracts or game-asset loaders.
If a fix requires those changes, record an exact finding/continuation for the
lead and finish independent in-scope work. Do not silently broaden scope.

Intentional decisions to preserve: no login/account/licensing UI or UI fallback;
one canonical UI; preview providers remain `online:false`; unavailable actions
reject or remain disabled. Preserve the accepted Auto/Manual cards, Manual empty
selection, encoded City alliance identities (including literal `none` and `all`),
Treasure persistence/defaults, search-on-click with no typing debounce, selection
lifetimes, tab/page cache, stale-request fencing, options redirects, Clear resets,
and summary/options ownership. Scheduling/share/claim/export actions must not be
made available merely to make screenshots match. Keep raw DOM differences such
as provider-fence attributes and encoded values visible in the evidence.

## Milestone A — finite baseline and renderer comparisons

Create `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/`.
Pin the immutable assignment-start source baseline before any correction. Recover
the actual original renderer and dependencies; execute production source for the
current side. Reuse existing accepted harnesses via task-owned adapters. Expand
the actual original filter components/icons: no hand-built reference markup or
stub that erases the very labels, images, selection states or geometry being tested.
Record hook/provider substitutions and their limits. Fix clock/timezone, inputs,
locale and CSS order (`reference.css`, then `styles.css`) on both sides.

Compare at least 34 toolbar states: all nine tabs in default state, plus all eight
normal kinds with source-valid configured filters/selections, each in EN and JA.
Configured states must exercise City alliance/marked-only; Resource/Monster name
options and keyword; Truck/Train quality, retained goods and plunderable filters;
Secret Task status/level/quality/delay/selected count; Ghost status/quality/delay;
Treasure type, foreign/lucky flags and claim controls. Scheduled scope includes
tab labels/counts and its toolbar/result message, not its job table body.

Also cover query-error/action-message presentation, loading and missing-server
control predicates, and source-valid pagination for zero/one-page totals and
first/middle/last pages of a multi-page total. Derive boundaries from the source,
not guessed numbers. Preserve raw DOM and semantic differences separately;
classify each with exact source locators. Do not normalize away mismatches or
require byte equality for intentional provider/identity-contract differences.
Commit this useful baseline/comparison milestone before further corrections.

## Milestone B — corrections and real local interactions

Fix every source-proven in-scope presentation/dropdown UI mismatch. For each fix,
retain baseline failure and demonstrate current success through actual original/
current rendering or rendered callbacks. Do not invent a production change when
the comparison matches. Replay keyword/name clearing, select and checkbox values,
retained-goods/Treasure dropdown open/select/close behavior, tab changes and page
controls through actual handlers. Keep accepted request/selection lifetimes intact.
Use inert search/options responses; no live export, scan, claim, schedule, share,
jump, mark, Run-now or gameplay operation. Do not invoke disabled native handlers.
Inventory the assigned translation keys across all nine catalogs without edits.
Commit coherent corrections/proof; continue to C unless a concrete blocker intervenes.

## Milestone C — paired browser proof and closeout

Capture at least six original/current browser pairs with identical inputs/CSS:
City EN/light desktop; City JA/dark at 375px; Truck EN/dark desktop; Secret Task
JA/light at 375px; Treasure EN/light desktop; Scheduled toolbar JA/dark at 375px.
Include multi-page pagination where applicable. Capture the remaining kinds in
the executable renderer suite; these six browser cases do not prove every branch.
Record actual viewport, fonts readiness, settled state, console, raw DOM,
rectangles/styles and screenshot hashes; visually inspect all twelve screenshots.
Use scoped Map containers with truthful ancestry and document limits. Check the
actual mounted clone for dropdown interactions, filter/tab switching and pagination
with inert/offline fixtures; do not rely only on static generated HTML for interaction QA.
If narrow layouts differ, preserve evidence and recover the source before fixing.

Acceptance requires every assigned case run and every difference explained; no
unresolved proven in-scope presentation defect. Intentional native availability
differences and unavailable native assets are explicitly recorded, not disguised
as exact parity. Pin reused dependencies and add a read-only evidence validator.
Replay affected accepted Map navigation, filter lifecycle, interactions, request
lifetime, Auto/Manual and table checks using current task-owned adapters as needed.
Do not overwrite historical results to make old extractors pass. Run canonical
`npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`, `build`, and
`check:production-build`, task validator, `git diff --check`, staged diff/allowlist
checks and the ten-file WIP guard. Preserve owner port 4335; clean only task-owned
helpers/tabs/storage, leaving owner state intact.

Write a dated review, coverage/difference matrix, replay README and exact
continuation. Update the relevant current docs conservatively to AWAITING_REVIEW;
do not declare full Map/global UI complete. Review/stage only owned changes,
commit, push and verify the full direct remote SHA. Return milestone commits,
checks, remaining differences, proof limits and preservation status. Stop after
this toolbar/pagination task; the lead reviews it. No fixed time block applies.

## Useful existing evidence

- `LWB317-UI-MAP-AUTO-REMOVE-001/milestone-b/compare-states.mjs`: actual original/current renderers and exact icon expansion.
- `LWB317-UI-MAP-AUTO-REMOVE-001/milestone-c/`: paired-browser capture/measurements and read-only integrity validation.
- `LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs` and `original/original-runtime.mjs`: exact original component execution.
- `LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs`: production callback/effect harness; later R1/output-redirect adapters preserve current semantics.
- `LWB317-UI-MAP-AUTO-CONFIG-001/`: maintained navigation/ownership replay adapters. Never rewrite its protected regression-results.json.
- `LWB317-UI-OFFLINE-VISUAL-001/current-inventory.md`: remaining page inventory; newer lead acceptances supersede older open items.
