# LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R1

Project-lead assignment, 2026-10-06. Status: **ASSIGNED**.
Parent CLOSEOUT-003: CHANGES_REQUIRED. Global UIUX: PARTIAL.

## Goal and startup

Finish the one demonstrated remaining recoverable UIUX correction, plus its
affected regressions and final integrated verification, in one continuous
assignment. Do not stop after merely documenting the defect or fixing one key.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Reviewed worker delivery: `5c0a5cb3c9e76fd573ee7facdebfd3bdd53a6758`.
Later lead review/assignment commits are expected: inspect actual HEAD/status;
never reset to that delivery or discard unrelated work.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, task.md, current UI master/checklist,
matrix/ledger/handoff, and
`docs/reviews/2026-10-06-LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-LEAD.md`.
Inspect the exact original contract and executable reproductions in
`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/lead-review/`,
especially `shell-afk/review.md`, its owner reproduction and the lead reproduction.

Reference EXE: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Original assets are under `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.
Canonical frontend: `src/LWBridge.UI-0.3.17/`.

## Proven defect and bounded production scope

Local 1 Potion minimum starts 50. An inert actual-input edit makes it 10000/dirty.
Switching the actual App to Local 2 wrongly retains 10000 and the previous error.
The exact original profile registry leaves Local 2 at its independent 50 default
and retains 10000 only for Local 1's return. The offline field remains disabled;
the reproduction tests the consumer ownership contract, not live functionality.

App currently passes `profileId` to Automation but not Squads. Four AFK child
store keys omit that owner. Propagate selected-profile identity through
App → Squads → AFK and implement the exact recovered owner domain for the AFK,
Potion, Garrison and Zombie child stores. Recover each contract before changing it.
The relevant original anchors are Squad `I` byte 28070 and main `me` 195132,
`re` 193714 and `T` 191297; verify asset hashes and slices yourself.

Preserve same-profile hide/return, A→B→A draft retention, acknowledgement/error
semantics, tabs, timers, React Activity cleanup and all native action fences.
Do not replace the module registry with global clearing. Do not assume Equipment
uses this owner domain; change it only if an exact source-proven omission exists.
No redesign, fallback, invented success, login/account UI or native provider work.

## Acceptance checks

1. Preserve an immutable baseline that reproduces the reported failure. Execute
   the original registry/hook contract and compare actual production consumers.
2. Prove A-edit → B-clean → A-retained → B-clean with unchanged fixture state,
   including invalid and valid drafts, confirmed values and Retry/Discard errors.
   Prove every source-profile-owned AFK child key, not just Potion.
3. Prove deferred save success/rejection cannot update another profile, rapid
   profile replacement and retained hidden/returned lifecycle. Verify App passes
   the owner into the actual mounted Squads composition. Keep offline controls
   disabled; label inert callback evidence honestly.
4. Recheck affected whole-AFK 30 compositions, 14 original/current pairs, Join
   11 checks, mounted AFK/Equipment lifecycle and Automation owner isolation.
   Replay accepted shell recovery/poll/profile-loading semantics. Use current
   adapters where a historical extractor is stale; never overwrite frozen records
   or repin old hashes merely to turn validators green.
5. Run a fresh complete-App integration against the final served source closure,
   covering the previous 267 assertions plus the newly distinguishing owner cases.
   Capture settled EN/light and JA/dark results and required affected responsive
   states; decode/inspect captures and record console/page issues. Preserve the
   same-input source/local comparison boundaries and bounded provider fences.
6. Run canonical frontend check, fresh build, production-package verification,
   evidence/source/screenshot integrity, unrelated-WIP preservation and diff checks.
   Unchanged Map/E–H/CSS/catalog inheritance may be established by pinned identity;
   explain precisely which checks were rerun and which were inherited.

## Delivery and autonomy

Use medium coherent checkpoints and continue automatically until this entire
assignment is complete or a concrete blocker is documented. Subagents are allowed
with bounded tasks, exclusive editable files and separate browser/process ownership;
one coordinator reviews/integrates their output and commits/pushes. Native/gameplay,
Last War launch/control, updater/OS actions, original-service access and auth bypass
are outside scope. Do not touch owner listeners or sessions. Lead port 4440 was
task-owned; inspect current listener ownership instead of assuming it is available.

Write new R1 evidence; preserve all historical and lead records. Update the current
master/checklist/matrix/ledger/handoff and dated delivery review conservatively.
Commit/push coherent milestones to origin/research/offline-controller, verify the
direct remote SHA and finish with a clean tree except preserved unrelated WIP.
Return AWAITING_REVIEW only after correction plus final checks. Report exact SHA,
proof counts/limits, commands, screenshots and continuation. Final UIUX acceptance
belongs to the lead; do not declare native functionality or full protected-runtime
pixel equality complete.
