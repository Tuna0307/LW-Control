# Milestone C — paired browser proof and closeout

This packet closes the assigned source/local Map toolbar and pagination gate. It
does not upgrade native action availability, Scheduled Plunder job-table bodies,
Map table-row scope, other pages, or the global UI status.

The six required original/current pairs use renderer output from Milestone B,
the same `reference.css` then `styles.css` bytes, fixed `Asia/Singapore` timezone,
device scale 1, matching language/theme/viewport, and truthful
`.main-view > .panel.map-panel > .map-search` ancestry. Normal table bodies are
excluded by assignment scope while their actual pagination remains a direct
`.map-search` child. `browser-pairs.json` pins every source/raw/generated file.

`capture-browser.mjs` records font readiness, console/page errors, raw scoped DOM,
rectangles, computed styles and screenshot hashes. All six pairs have zero
geometry differences and all twelve screenshots have zero console/page issues.
City EN/light, City JA/dark 375px and Scheduled JA/dark 375px are pixel-identical.
Truck, Secret Task and Treasure differ only in the accepted unavailable-action
fences: one Truck schedule button, two Secret Task schedule/share buttons and two
Treasure claim buttons. `record-inspection.mjs` records the manual inspection of
all twelve images; `difference-matrix.json` reports zero unresolved in-scope
presentation defects.

`run-regressions.mjs` replays nine affected groups against current source. Stale
historical harness shapes are not rewritten: navigation uses the accepted current
shape adapter, filter lifecycle uses the accepted R1 redirect, Manual uses the
superseding empty-selection checker, and current table rendering uses the actual
original/current 48-case offline renderer. Unconditional historical writes are
redirected under this task's `replays/` directory. No native/gameplay action runs.

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/prepare-browser-pairs.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/capture-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/record-inspection.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/run-regressions.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/validate-evidence.mjs
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
npm.cmd --prefix src/LWBridge.UI-0.3.17 run build
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/check-protected-wip.mjs
```

Milestone commits before this closeout are `4574cc8aaf5751b1089c22f997e580d0ad16dff8`
(immutable baseline/comparison) and `9668133737f2589c27224613d8d343c5e732e474`
(presentation corrections and real local interactions). Project-lead acceptance is
still required; task status is `AWAITING_REVIEW`.
