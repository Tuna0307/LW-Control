# LWB317-UI-CORRECT-003A-R1 — save weekly edits immediately

Project-lead assignment: 2026-10-01. State: COMPLETE / ACCEPTED by PM-010.
One small correction to CORRECT-003A; no other feature work.

Lead reviewed d065d08 and independently reran actual-handler, evidence and package
checks. Acceptance is limited to this source-backed/local weekly correction;
original pixels/native persistence and parent CORRECT-003 remain unaccepted.

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Reviewed worker baseline:
2f439ced3b867fcb504f48fb081aefccea980eaf.
Start at the latest lead checkpoint containing this assignment; preserve WIP
and newer commits. No reset/clean/force-push.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this file and
docs/reviews/2026-10-01-LWB317-PM-009-weekly-lead-review.md. Do not restart the
parent CORRECT-003 audit or reimplement completed weekly controls.

## Fix

Original AutomationPanel-BJ0gIqFh.js at UTF-8 bytes 33073/27880 routes weekly
day changes through W: patch with debounce disabled, then immediate flush with
handled rejection. Its SHA-256 remains
6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
The exact reference and executable hash remain as specified in AGENTS.md.

Pages.jsx weekly callbacks at reviewed lines 430 and 442 call only store.edit,
waiting for the default timer or blur. Make both paths match the original
immediate write behavior. Keep selectors editable during config saving. Do not
change the shared store's default debounce or any other controls/providers.

Test the actual production onChange callback/helper, not a test-authored sequence
that adds flush independently. The lead AST callback reproduction in
evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003A/lead-review/
check-weekly-onchange.mjs currently fails for both cards. Make actual-handler
checks pass, including a second edit while a reply is deferred, correct older-ack
handling, failed write and retry/discard. If extracting a helper, resolve/import
that actual helper in the harness; do not substitute a handwritten callback.

Fix the two shared-error locator records identified in PM-009 by using exact
excerpt-start offsets or explicit anchors. The lead evidence verifier must pass.
Preserve the original failure reproduction JSON as historical evidence.

## Completion boundary

Use local preview/offline tests only. Do not launch/control Last War. No Trade,
Assist, AFK, Map, other Automation forms, backend, login/licensing, fallback or
subagent work. Keep uncommitted AFK fixtures/scratch/parent screenshots unchanged.

Record a short R1 review and focused evidence. One browser check per weekly card
is sufficient to confirm selection and existing Retry/Discard still work;
prove dispatch/concurrency through the actual-handler deferred test. Reuse
existing screenshots when they still apply, explicitly as existing evidence.
No broad screenshot campaign or unrelated runtime tests.

Run canonical npm check/build/check:production-build, focused actual-handler/store
checks, lead evidence verifier and git diff --check. Update only weekly status
and continuation notes conservatively; full UI/native parity is not accepted.
Commit/push only this correction and its evidence to origin/research/offline-controller,
verify remote identity and report unrelated WIP. No fixed clock deadline.
Return AWAITING_REVIEW to the project lead, then stop at this single correction.
