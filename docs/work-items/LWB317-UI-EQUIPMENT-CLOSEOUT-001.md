# LWB317-UI-EQUIPMENT-CLOSEOUT-001 — medium Equipment-only task

Status: ASSIGNED. Work alone: no subagents or delegation. The owner's permission
for project-lead subagents does not apply to this worker chat.

## Start and intent

Repository: C:/Users/chimw/OneDrive/Desktop/Github/LW-Control.
Branch: research/offline-controller. Last implementation closeout:
7e7eb1230e295300841bbdedb5c3adb3c622f08b. A documentation-only dispatch commit
follows it. Inspect the actual HEAD; do not reset newer work or restart AFK.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, task.md, docs/PROJECT_LEAD.md,
docs/UI_FINISH_CHECKLIST.md and this work item. Inspect git status before edits.
The goal is source/local 1:1 UI recovery before native function integration.
Quality takes priority over today's delivery target. No fixed time block.

Exact reference:
C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Original assets: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/.
Begin with SquadPanel-HC3-DJei.js, its imports, exact CSS/locales and
GameAssetImage-Diy9VTIr.js. Recover identities and UTF-8 locators directly.

Current EquipmentContent is in src/LWBridge.UI-0.3.17/src/Pages.jsx.
Earlier source/QA: docs/reviews/2026-10-01-LWB317-UI-CORRECT-001.md,
and that task's source-manifest.json, coverage-matrix.md and interaction-qa.md.
They are historical guidance, not proof of the current implementation.

## Finite scope

Audit and fix only the Equipment Schemes tab within Squads/AFK:

1. Scheme selection, selected identity, dirty state, summaries, empty state and
   current-equipment matching presentation. Recover what original inputs exist;
   synthetic hero/item labels are not proof of original game data.
2. Existing rename dialog: initial value, trim/blank validation, save/cancel,
   Enter/Escape/backdrop, busy/disabled and close behavior. Compare actual original
   and production handlers/renderers, including negative cases.
3. Existing item, whole hero-loadout and squad-loadout move/swap interactions:
   correct slots, identity/order, wrong-slot rejection, no-op/self-drop,
   drag-target/success/error/toast cleanup and timers. Recover actual rules;
   do not add a new interaction or keyboard shortcut by assumption.
4. Existing partial-result/apply-progress/error and action-enabled presentations.
   Exercise source-consumed supplied state inertly. Native read/apply/refresh/
   save-and-apply remain unavailable; no successful native provider is added.
5. Equipment → AFK → Equipment round trip: prove intended local state retention
   and ensure this task does not regress the accepted AFK/member/draft work.

Start with a compact source-located checklist of these five groups. Existing
matching behavior needs verification, not replacement. Fix demonstrated defects
only; preserve original labels/defaults/order/timing and canonical CSS. Keep
accepted Automation, Trade, weekly, Home and Map behavior unchanged. No redesign,
legacy fallback, login/account/licensing UI or new backend/native campaign.
City Layout, Hotkeys, Mini Games, Settings and shell-wide integration are separate.

## Verification and evidence

Use exact original functions/expressions as oracles for critical render, predicate,
handler and transformation cases. Test real production callbacks and payloads.
Marker-only checks and fixtures checking their own constants are insufficient.
Preserve an immutable failing baseline for each correction where executable.

Browser-check real rename, selection and navigation in English/light and
Japanese/dark. Inspect a narrow layout with the actual viewport width recorded.
Save and visually inspect meaningful settled screenshots; capture fresh console
warnings/errors. Distinguish physical HTML5 drag from inert handler/DOM-dispatch
proof. If the connector cannot drive physical drag, document that limitation;
continue source/handler proof and do not claim a physical pass.

Do not open native window.confirm dialogs during this task: the lead's prior
fixture Delete confirmation surfaced late in the owner's app. If an Equipment
delete confirmation is recovered, verify cancel/accept through inert actual
callbacks. Do not click original/gameplay/function controls or launch Last War.
Do not close or repurpose existing owner tabs/processes/listeners. Own and clean
up only your temporary test tabs, preview process and disposable test storage.

Write evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001/ with source
manifest, fixed five-group coverage matrix, actual comparison results, baseline,
browser records/screenshots and executable validator. Write a dated review.
Update this work item and current handoff/matrix/ledger conservatively as
AWAITING_REVIEW for this unit; the lead alone accepts it or advances global status.

Run focused Equipment checks, any directly affected AFK regression, and canonical
npm.cmd run check, build, check:production-build from src/LWBridge.UI-0.3.17.
If Pages changes, the previous AUTOMATION-AFK-CLOSEOUT manifest intentionally pins
the prior Pages hash: run its actual check-closeout.mjs and renderer checks against
current production; preserve its historical manifest/validator rather than editing
it to manufacture a pass. Record expected hash staleness separately from real
behavior failures. Do not repeat expensive unaffected Map campaigns.

## Preservation and delivery

Preserve unchanged and unstaged all seven paths in
evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/protected-wip-before.json.
Run its check-protected-wip.mjs before and after. This includes both historical
filter result files, previewAfkFixtures.js, .scratch-lwb317 and parent CORRECT-003
screenshots. Use a task-owned fixture adapter if necessary. No reset, stash,
clean, broad stage, force-push or historical evidence deletion.

Work sequentially: recover/verify the five groups, implement demonstrated fixes,
then browser/evidence closeout. Commit/push coherent milestones, review the diff,
run diff and staged checks, verify origin/research/offline-controller directly.
Return AWAITING_REVIEW with changed behavior, proof types/limits, remaining exact
gaps, results, evidence/review paths, commit/full remote SHA and protected status.
If interrupted, record the exact next case/command. Stop at Equipment; do not
start another page or wait for a new reviewer inside this worker task.
