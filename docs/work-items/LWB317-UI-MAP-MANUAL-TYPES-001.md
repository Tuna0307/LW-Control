# LWB317-UI-MAP-MANUAL-TYPES-001

Status: AWAITING_REVIEW through owner relay, 2026-10-04. Size: small.
Baseline: cd3b0bbe880c94351621185e9c2e1d0151686b36.

## Goal and evidence

Fix only MMV-BEHAVIOR-001: the original permits deselecting the last Manual
scan type; current disables it and preserves it in updateSelectedTypes.
Original checkbox/onChange: MapDataPanel-B4GXEND2.js UTF-8 bytes50063/50135.
Current: MapDataPage.jsx line1085 and mapBackend.js updateSelectedTypes.
Review docs/reviews/2026-10-04-LWB317-UI-MAP-MANUAL-VISUAL-001.md and that
packet's selection-counter-evidence.json/check-selection-counter-evidence.mjs.
The actual inert baseline proves original [] versus current [city].

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md and docs/lwbridge-ui.md.
Work alone, no subagents. Use research/offline-controller. Verify reference EXE
and original asset identities from the existing packet before recovering facts.

## Allowed changes

Only Manual checkbox disabled behavior and its local selection-update helper,
plus the affected assertion in scripts/check-map-integration.mjs (line65 currently
asserts the incorrect last-type rule). Own new focused evidence/review/work-item
delivery records. Do not alter Auto's separate last-type protection, native
availability fences, timing, other Map workflows, CSS, label markup or providers.

Recover source behavior before changing the helper. Editing state must support
last-type removal to [], then selecting one type from [] without repopulating all
types. Preserve source-backed editing order. Keep normalizeSelectedTypes and
buildStartPayload's separate request-boundary behavior unchanged; prove the
original empty-selection start normalization by executing its pure helper,
without invoking a scan. Do not merely remove the JSX disabled predicate while
leaving either last-type preservation or empty-to-all editing normalization.

## Checks and delivery

Create a small actual-original/current inert callback comparison covering
single->empty, empty->single, adding/removing another type and source ordering.
Preserve the failing baseline; prove corrected behavior. Verify Auto still has
its recovered last-type predicate and no Manual edit changes its config.
Use a real offline browser to deselect all Manual chips and select one again;
save one inspected screenshot plus results and console record. No Start/Stop/
Clear click, Last War launch, native action, auth bypass or new provider.

Run focused check, npm.cmd --prefix src/LWBridge.UI-0.3.17 run check, build,
check:production-build, git diff --check, and the existing ten-file WIP guard
under HOME-PREFERENCE-LIFETIME-001B without --record. Preserve all starting WIP.
Keep parent visual/historical evidence immutable; its source-hash validator and
counter-evidence assertion intentionally describe the pre-fix code. Put any
needed adapter/current evidence in this new packet, not in the parent.

Write evidence under evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-TYPES-001/
and a dated review. Update this item's delivery, review the explicit owned diff,
commit, push and verify full remote SHA. Preserve owner port4335; clean up only
owned helpers. Stop after this correction and return AWAITING_REVIEW with files,
checks, remaining limits and exact SHA. The lead handles acceptance/master status.
No additional visual campaign or fixed elapsed-time stop is assigned.

## Worker delivery — 2026-10-04

Manual final-type deselection and empty-selection editing are corrected in the
allowed JSX/helper paths. Actual original/current callback evidence covers
single-to-empty, empty-to-single, add/remove/re-add ordering, and proves Manual
editing emits no Auto config update. The original pure Start normalizer and
current request-boundary helper both map empty selection to all eight types;
Auto's separate last-type predicate remains present.

Focused offline Chrome cleared all eight Manual types and selected Resource
Point alone with zero console issues; one inspected screenshot and results are
saved in the new evidence packet. Canonical check/build/package, diff check and
the ten-file protected-WIP guard pass. Dated worker delivery:
`docs/reviews/2026-10-04-LWB317-UI-MAP-MANUAL-TYPES-001.md`.
The exact pushed SHA is reported in the worker relay because this delivery file
is part of that commit. Project-lead acceptance/master updates remain separate.
