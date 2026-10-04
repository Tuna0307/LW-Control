# LWB317-UI-MAP-AUTO-REMOVE-001 review — 2026-10-04

Status: **AWAITING_REVIEW**

## Source identity and scope

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`,
SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The original Auto card is pinned to `MapDataPanel-B4GXEND2.js` bytes
46001-48950 and its remove-icon call at UTF-8 byte 47234. The recovered shared
icon component is pinned at index asset UTF-8 byte 330489; `remove` renders
`svg.ui-icon`, viewBox `0 0 16 16`, `aria-hidden=true`, `focusable=false`, path
`m4 4 8 8M12 4l-8 8`.

Production scope stayed inside the assigned Auto-card renderer in
`src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`. The server-chip button label,
attributes and callback are unchanged, existing `ui-icon` CSS is reused, and no
App/backend/Auto-executor/helper/CSS redesign was introduced.

## Review findings

Milestone A, commit `8c28f5a77338fa2716e43c3f2ab68941febebcf1`, replaces the
text `×` child with the exact recovered SVG. The configured Japanese/dark 375px
original/current pair is exact in source render, scoped markup, browser DOM,
geometry, computed style and PNG bytes. Its screenshot hash on both sides is
`b9fa3c3e1eb8cf52e96c51354154323a94f386fc8764dca920ed717e7f8f1acd`.
The accepted pre-fix AUTO-VISUAL-001 packet remains unchanged and continues to
show the historical text-glyph difference.

Milestone B, commit `f9a63fa5fbde28babdb1d8862f46e897e0751524`, executes the
actual recovered/current renderers for recovered default, configured waiting,
Auto running, Manual scan reading, offline and one remaining Auto type in both EN
and JA. All 12 pairs have exact presentational views, exact scoped markup and zero
differences. Source pins cover type order, the last-type disabled predicate and
Run-now predicate. Direct inert callback replay matches in both languages: Add
produces `[8,15,120]`, Enter/deduplication produces `[8,15,120,7,9]`, and removing
15 produces `[8,120,7,9]`. No native/gameplay mutator is called and Run now is
never invoked.

Milestone C, commit `30d409f0fb2ec4f079d7908063529a319c81c42d`, captures the
four assigned browser pairs: EN/light default 1280x720, JA/dark configured
375x1000, JA/light running 375x1000 and EN/dark offline 1280x720. Raw Auto-card
markup is exact in all four. Browser DOM, measured rectangles, computed styles
and console state have zero differences/issues, and every original/current pair
has byte-identical PNGs. All eight screenshots were inspected. Pair hashes are:

- EN/light default: `34e3f61e8bcba907cab550c64febbdc83f542b651e1616451a5088db454c47d7`;
- JA/dark configured: `b9fa3c3e1eb8cf52e96c51354154323a94f386fc8764dca920ed717e7f8f1acd`;
- JA/light running: `4e8d48fb3ff7255f68d25e5b1b7b03caf17ce30f3ccd2011b5890b04dfddbd6e`;
- EN/dark offline: `3df6195450b1782786382079cf00325b9217ee340928cb39b741e5e1d8e3d622`.

No additional Auto-card presentation defect was proven in B or C, so there is no
further production correction. No broader defect is asserted from this bounded
packet; surrounding Map/shell/native-runtime behavior remains outside this work
item.

## Evidence and checks

Durable evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/` in the milestone A,
B and C directories. Each milestone has a replay README and integrity validator.
Milestone C additionally pins the four exact state/config inputs, clock/timezone,
source locators, dependency hashes, raw markup differences, browser rectangles,
computed styles, console records, screenshot hashes and inspection records.

Focused validators pass for A, B and C. The HOME-PREFERENCE-LIFETIME-001B
ten-file WIP guard passes without `--record`. Canonical frontend check, canonical
production build/package integrity and `git diff --check` are rerun at delivery.
Owner port 4335 is untouched; C uses task-owned ports 4344/4345. Starting unrelated
WIP and historical evidence remain preserved.

## Proof limits

This is isolated source/local Auto configuration-card evidence. Browser captures
render the recovered/current scoped card with exact recovered CSS and current
production CSS order; they are not a claim of full original executable shell or
native-runtime pixels. No live Run-now, scan, server-jump, native-provider or
gameplay action was authorized or executed. Overall Map/UI status therefore stays
unchanged pending lead review.

## Delivery

Work item: `LWB317-UI-MAP-AUTO-REMOVE-001`

Status: `AWAITING_REVIEW`

Reference: LWBridge 0.3.17 EXE SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Files changed: one bounded production renderer correction, task-owned A/B/C
evidence/adapters, this focused review, work-item status and current handoff note.

Facts established: exact recovered remove glyph restored; assigned state/control
and browser comparisons match the recovered card after the correction.

Still unknown: full original-runtime/shell pixels and native/gameplay behavior
outside this source/local Auto-card gate.

Milestone commits: A `8c28f5a77338fa2716e43c3f2ab68941febebcf1`; B
`f9a63fa5fbde28babdb1d8862f46e897e0751524`; C
`30d409f0fb2ec4f079d7908063529a319c81c42d`.

Recommended next task: none from this worker; return to the lead for acceptance
and next-page/work-item selection.
