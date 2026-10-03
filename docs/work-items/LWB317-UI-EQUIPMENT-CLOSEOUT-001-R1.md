# LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1 — medium Equipment correction

Status: COMPLETE / ACCEPTED for bounded recovered-source/local scope by the dated
project-lead REVIEW-EQUIPMENT-CLOSEOUT-001-R1 review of 2e4acce/32b2906.
Native/pixels/physical drag unproved. Original worker assignment/delivery retained.

## Start

Repository: `C:/Users/chimw/OneDrive/Desktop/Github/LW-Control`.
Branch: `research/offline-controller`.
Reviewed implementation: `4bafcd437929f2f905b557ca700657467afbdcbc`.
A documentation/evidence-only lead review follows it. Inspect current HEAD and git
status; do not reset newer commits or overwrite unrelated WIP.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/UI_FINISH_CHECKLIST.md, this work item, the parent Equipment work item, and
docs/reviews/2026-10-03-LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001.md.
The original reference remains LWBridge 0.3.17 at
`C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe`, SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Original assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.

## Only three acceptance gates

1. Suspend hidden Equipment/AFK effects while retaining visited state. Original
   Squad `pd` byte 191313 uses `React.Activity`; current `display:none` wrappers
   do not remove Equipment's window Alt listener. Match the original boundary
   using installed React Activity, or prove another implementation equivalent.
   Actual visible Alt+1–4 behavior and input/repeat/busy exclusions must remain.
   Verify Equipment → AFK blocks the hidden shortcut without preventing default,
   Equipment return restores exactly one listener, and selected/dirty/renamed/moved
   state survives. Check modal cleanup/reappearance, toast/drop timer cleanup and
   affected AFK effects through actual mounted lifecycle rather than marker tests.
   Do not redesign unrelated Automation/category lifetime.
2. Recover and implement the existing Equipment rename local acknowledgement
   lifecycle. Original `fd.ye` byte 180434 waits for `P`/draft flush: pending busy
   with dialog retained, success closes, rejection/null retains dialog and clears
   busy. Current happy callback synchronously confirms all presets and closes.
   Use actual production draft/callback code with a controlled local acknowledgement
   path, following the existing preview config patterns where appropriate. No
   native Equipment provider is added. Test pending/success/rejection, blank and
   unchanged name, already-dirty preset, Enter/Escape/busy rules and unrelated
   preset preservation. Recover exact failure/retry/discard presentation and any
   save-only consumer shared with rename before editing; do not invent controls.
   Preserve read/apply native fencing. A static busy fixture alone is insufficient.
3. Write an executable validator for the new R1 packet: verify reference EXE,
   original asset and exact byte slice hashes, current production/evidence JSON,
   meaningful screenshot hashes, immutable submitted failing cases and protected
   WIP. Keep prior worker/lead evidence intact. Historical current-product hash
   validators may become stale by design; report that separately from behavior.

The lead reproduction is in
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/check-review.mjs`.
It intentionally asserts the submitted defect and missing branch. Preserve it;
create new distinguishing original/submitted/corrected proof for R1. The parent
105-assertion checker and 729-case AFK regression already pass; reuse them rather
than reopening the whole Equipment audit.

## Verification and limits

Run the R1 actual effects/callback/deferred acknowledgement cases, parent Equipment
checker, affected AFK checker/Join/Assist, canonical `npm.cmd run check`, `build`
and `check:production-build` from `src/LWBridge.UI-0.3.17`, new evidence validator,
diff/staged checks and protected-WIP guard. Update stale harness adapters honestly
if Activity/draft dependencies change extraction; preserve historical results.

Browser-check real Equipment → AFK → Equipment keyboard behavior, retained dirty
state and rename pending/error/success in English/light and Japanese/dark. Capture
fresh console warnings/errors and inspect screenshots that actually expose the
changed states. Use explicit offline preview-only controlled acknowledgement;
never fake native success or introduce live functionality. Record exact viewport.
No native window.confirm, original protected-runtime bypass, Last War launch or
gameplay/provider actions. Preserve owner sessions and clean only owned helpers.

No City Layout, Hotkeys, Mini Games, Settings, shell-wide integration, native phase,
login/account/licensing UI, redesign or fallback. No deadline-based shortcuts.
Physical HTML5 drag and original pixels remain disclosed limitations, not required
new investigations in this R1 correction.

## Preservation and delivery

Preserve all seven protected paths pinned by
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/protected-wip-before.json`;
run its `check-protected-wip.mjs` before and after. No reset/stash/clean/broad staging
or force-push. Do not edit previewAfkFixtures.js or .scratch-lwb317.

Write `evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/` and a dated
R1 delivery review. Update this work item/current handoff/matrix/ledger as
AWAITING_REVIEW, leaving parent CHANGES_REQUIRED until project-lead acceptance.
Commit coherent milestones, push origin/research/offline-controller and verify its
full SHA directly. Return changed behavior, checks/evidence, proof limits, commit/
remote SHA and exact continuation if interrupted. Stop at R1; do not start a page.

## Worker delivery — 2026-10-03

Implementation/evidence milestone:
`2e4accef7b0709a903034293126cb63fe389b105`.

The visited AFK/Equipment boundary now uses installed React Activity so hidden effects
are suspended while component state is retained. Equipment rename and the save-only
consumer now share a controlled local acknowledgement path with pending, success,
rejection, Retry and Discard behavior matching the recovered frontend contract.

The R1 executable proof records 61 assertions. Parent Equipment remains 105/105 and
the affected AFK regression remains 729/729 with exact Join/Assist renderer recovery.
Canonical check/build/production-package checks pass. The R1 evidence validator passes
71 integrity assertions and pins the reference EXE, exact recovered byte slices,
current production/evidence hashes, screenshots, immutable submitted failure packet
and all seven protected-WIP paths.

Real mounted browser QA covers Equipment -> AFK -> Equipment effect cleanup/reinstall,
retained selected/dirty/renamed/moved state, dialog/timer lifecycle, affected AFK
Escape handling, and pending/error/success acknowledgement states in English/light
and Japanese/dark. Fresh console capture has zero warnings/errors. Native Equipment
providers/persistence/gameplay, physical connector HTML5 drag and original post-auth
pixel equivalence remain outside this correction.

Parent `LWB317-UI-EQUIPMENT-CLOSEOUT-001` remains **CHANGES_REQUIRED** until the
project lead accepts this R1. Do not start City Layout or another page from this
worker delivery.
