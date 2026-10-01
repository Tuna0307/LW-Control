# LWB317-UI-CORRECT-003A — finish weekly quality settings only

Project-lead assignment: 2026-10-01. State: CHANGES_REQUIRED after PM-009.
This replaces the active broad CORRECT-003 assignment with one smaller unit.
The parent remains PARTIAL; do not resume its other work during this task.

Worker delivery 2f439ce retains useful weekly save-lock corrections, but PM-009
found both actual weekly callbacks still debounce instead of writing immediately.
Active correction is LWB317-UI-CORRECT-003A-R1; do not broaden this task.

## Context and inputs

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Worker implementation baseline:
5204f6734a1b652610e2c3639ca1b818a44fe25a, already pushed to origin.
Start from the newest lead checkpoint containing this assignment. Preserve
newer commits and inspect WIP; never reset, clean or force-push.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this assignment and the current
weekly controls in src/LWBridge.UI-0.3.17/src/Pages.jsx,
previewAutomationContracts.js and previewConfig.js/previewConfigHook.jsx.
Use the current UI master as needed. Do not restart the entire historical audit.
PM-008 records the inspected interruption and untouched work.

Exact reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Original weekly render/default/write/disabled rules are in AutomationPanel-BJ0gIqFh.js
under evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/.
Its SHA-256 is 6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Trace its imported config helper only where needed for these controls.

Uncommitted weekly fixture edits already exist in previewAutomationContracts.js.
Inspect and keep/correct them only as needed for this scope. The AFK fixture edits,
.scratch-lwb317 and other CORRECT-003 work belong to later continuations: preserve
them unchanged and exclude them from this task's commit. Do not discard WIP to
obtain a clean status. Inspect any existing preview processes/tabs before using
them; do not stop an unidentified or owner session.

## One deliverable

Finish and verify ONLY the seven-day quality settings in Trucks and Secret Task,
including their draft save/error/retry/discard behavior. Existing implementation
should be reused; do not rebuild unrelated UI or redo the full CORRECT-003 review.

1. Record original defaults, weekday order, options, edit/write and disabled rules
   with exact source hash and byte/handler locator. Check existing implementation
   against those rules and make only necessary corrections.
2. In the local browser preview, verify both cards' initial seven values and an
   edit to one day. The other days/card must remain unchanged. Check the source-
   prescribed behavior when settings collapse, category changes and navigation
   occur; do not invent disk/native persistence.
3. Exercise save failure after an actual edit. Retry must confirm the edited value;
   a separate discard case must restore the confirmed value. Do not fake success
   or create failure merely by an unrelated toggle if that masks the weekly edit.
4. Verify running/saving/offline disable behavior and invalid weekly-array rejection
   against source. If a saving state is too brief for physical browser observation,
   test the actual adapter/store with a deferred reply and disclose that limitation.

## Boundaries

UI/local preview/offline checks only. No Trade, Assist, AFK, Map, Home lifecycle,
other card completion, real task execution or Last War launch/control. No native
provider/service, login/licensing UI, fallback, auth bypass, subagent or broad
cleanup. Exact CSS/assets stay unchanged. Preview fixtures remain disclosed
synthetic QA data and cannot be selected by native modes.

## Checks and return

Create docs/reviews/2026-10-01-LWB317-UI-CORRECT-003A.md and a small evidence folder
at evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003A/ containing source locators,
actual interaction outcomes and screenshot hashes. Two useful screenshots
(edited settings and save-error state) are sufficient; no full-page QA campaign.
Run npm check/build/check:production-build from the canonical UI project, focused
actual-state checks where needed and git diff --check. Do not retest unrelated
runtime/game systems. Update only the relevant weekly coverage and continuation
notes, preserving the parent's unvalidated status.

Commit/push only this task's coherent changes to origin/research/offline-controller;
verify remote identity and report any unrelated WIP left intact. No fixed clock
stop: complete this small unit or document a concrete blocker. Then return
AWAITING_REVIEW to the project lead with checks, evidence, commit, remaining WIP
and exact continuation. Stop after this unit; do not resume the larger task.
