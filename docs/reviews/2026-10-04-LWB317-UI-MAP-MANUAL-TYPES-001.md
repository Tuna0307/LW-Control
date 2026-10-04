# LWB317-UI-MAP-MANUAL-TYPES-001 — worker delivery

Status: **COMPLETE / ACCEPTED** for focused source/local Manual editing scope.

The focused Manual scan-type discrepancy is corrected. The Manual checkbox no
longer disables the final selected type, and `updateSelectedTypes` now treats
empty selection as a valid editing state. Deselecting the last type therefore
produces `[]`; selecting Resource afterward produces only `["resource"]`.
Additional add/remove/re-add steps preserve the recovered source callback's
append/filter order.

The request boundary is unchanged. `normalizeSelectedTypes` and
`buildStartPayload` retain their empty-to-all behavior, matching the recovered
original pure `rt` helper before Start. The Auto Scan checkbox still carries its
separate last-type protection, and the focused callback run records zero Auto
config edits from Manual interactions. Native availability/action fences and
all other Map markup/workflows are unchanged.

Reference identity was revalidated before recovery: EXE SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
and original `MapDataPanel-B4GXEND2.js` SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
The original Manual callback remains at UTF-8 byte 50135; the pure selected-type
normalizer is at byte 14384. The accepted pre-fix `[]` versus `["city"]`
counter-evidence is copied unchanged into the new packet.

Evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-TYPES-001/`.
`check-manual-selection.mjs` executes seven actual original/current Manual
callback states and the original/current pure Start normalization. The real
offline Chrome check clears all eight Manual types and then selects Resource
Point alone. Its console record has zero issues, and the inspected screenshot is
`browser/manual-empty-to-resource.png` with SHA-256
`B3372AE5D17CC907980DA31C2609C733B026F2CEDCDE19C09359D01AB3A85C03`.
No Start/Stop/Clear/native action is invoked.

Checks passed:

- focused callback/original-helper checker;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`;
- `git diff --check`;
- HOME-PREFERENCE-LIFETIME-001B protected-WIP guard, ten of ten preserved.

Production build/package hashes are
`865208F65150F67AA0F3A29CD8E6C0B97352181E68E7047CAD47C6DF87866EA2`
and `7F3BBBF98BD110E0B15B413A4E52D4D3C281ED076F856830CD64395FABB64A90`.

Remaining limits: this establishes recovered source/local Manual editing parity
and request normalization only. It does not run a native scan, launch Last War,
or upgrade protected-original pixel/live-function status. Project-lead review
owns acceptance and any master-status update.

## Project-lead review — 2026-10-04

Accepted delivery 6569fa108ae3a14f681dd00c75d99c4f87d4a21b; direct remote matches.
Only the allowed Manual input predicate, editing helper and affected assertion
changed in production. Auto's separate predicate, normalizeSelectedTypes and
buildStartPayload are unchanged in the diff. Lead rerun confirms seven matching
callback states and zero Auto-config edits. The lead additionally executed rt([])
from the actual original module with its original bound constants, confirming
the unchanged empty Start normalization; no Start action was invoked.

Seven recorded source byte slices, preserved baseline copy and screenshot hash
verify. The saved offline image was visually inspected: only Resource Point is
selected. Canonical check, production-package check, diff check and ten-path WIP
guard pass. No additional production fix or broadened test campaign is needed.
The focused defect MMV-BEHAVIOR-001 is closed. Browser evidence is worker-run
and lead-inspected; native and complete Map/original-runtime pixels remain outside
acceptance. Historical pre-fix visual records retain their original pinned hashes.
