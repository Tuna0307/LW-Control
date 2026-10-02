# Map refresh ownership acceptance and Auto configuration dispatch

Date: 2026-10-03. Project-lead decision.

## Reviewed delivery

Accept cf75b4e3cd8b602c17cef9a244385e6db7c02daf for the focused
LWB317-UI-MAP-REFRESH-OWNERSHIP-001 source/local UI scope. The actual
App.jsx/MapDataPage.jsx/mapPreviewApi.js diff was inspected. Original parent
summary and panel options boundaries are pinned in milestone-b/source-locators.json:
index bytes 363957/370275; panel 34224/34459/34913/35085/35643. The completed
ownership proof executes actual App callbacks/effects and the current controlled
panel, rather than treating a source regex as runtime proof.

Lead reruns on current delivery passed:

- Ownership: parent requests 2, controlled child summary 0/listeners 0;
  options 321,321. Deferred cleanup/profile/overlap and count-reset cases pass.
- Focused delayed Clear: 322,321.
- Exact R1: 5/5; independent PM-027: 6/6, preserving 321,322,321.
- Request lifetime: 38 scenarios, zero current failures; interactions:
  38/38 original/current parity, 31 baseline mismatches retained.
- Evidence validator: 7 locators, 2 browser records, 6 protected pins.
- npm.cmd run check and check:production-build. Package fingerprints match
  5d8d24301b1845f22cdda69ede0c0cd06560bc351e597a48a184213f977c4a78 /
  91345a1b8793ff573b78ba7898265c040817d302548382dc5c6542a5360dde3e.

No acceptance-blocking defect was established. The worker's source-backed
count-readiness correction is valid: disappearing/mismatched parent summary
must retire displayed ready counts. No further production change was made by
the lead. Browser observations/screenshots are worker evidence; this lead
review did not independently repeat browser capture or perform a fresh build.
Current package verification passed. Historical fixed-shape extractors remain
historical; their known incompatibility is not represented as current PASS.

Acceptance does not establish original post-auth pixel parity, native producers,
gameplay or full Map/UI completion. Parent MAP-CLOSEOUT-002 remains PARTIAL.
Historical review/evidence and pre-existing unstaged work are retained.

## Next assignment

Assign LWB317-UI-MAP-AUTO-CONFIG-001: complete Auto Scan UI/configuration through
exact helpers, profile-aware parent state/persistence, all controls and integrated
proof. Its full worker prompt is in docs/work-items/LWB317-UI-MAP-AUTO-CONFIG-001.md.
Source recovery at a6140b2 is reused; native Auto executor work remains outside
this campaign. The source-recovery packet name agent-b is historical only.

The owner's new direction is mandatory: one worker working sequentially, no
subagents. AGENTS.md and AI_WORK_PROTOCOL.md now state this and supersede prior
permissions. The owner continues manual relay; project lead retains acceptance.
