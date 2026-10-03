# LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001 — medium residual completion

Status: ASSIGNED. Single worker, no subagents. Owner priority is to finish UI/UX
today if feasible; keep this assignment bounded and checkpoint sequentially.

## Start and goal

Repository: C:/Users/chimw/OneDrive/Desktop/Github/LW-Control.
Branch: research/offline-controller. Production baseline ec2757742ee404dcadb4ac88281c1cc25a052d19;
the dispatch arrives as a documentation-only descendant. Do not reset newer work.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, task.md, docs/PROJECT_LEAD.md,
docs/UI_FINISH_CHECKLIST.md and this assignment. Assume a fresh chat. Work alone:
no subagents, delegation or another worker chat. Inspect git status and inventory
all protected WIP before editing.

Finish only the unresolved Automation and Squads/AFK branches from the earlier
CORRECT-003 campaign. Much of the code already exists; verify current code,
correct real mismatches and finish its missing evidence. Do not execute the old
entire CORRECT-003 assignment or repeat accepted weekly/Trade/Map/Home campaigns.

Target EXE C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe;
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Exact assets under evidence/lwbridge-0.3.17/ui/frontend-package/web/assets:
AutomationPanel-BJ0gIqFh.js, SquadPanel-HC3-DJei.js, index-BVfnK1wp.js and
GameAssetImage-Diy9VTIr.js, CSS and locales. Revalidate exact hashes/UTF-8 locators.

## Inputs and intentional behavior

Read CORRECT-002/coverage-matrix.md, CORRECT-003/source-contracts.json and
check-state.mjs, dated CORRECT-003 review and PM-008 interrupted checkpoint.
These describe earlier code, not necessarily current gaps. Accepted weekly
003A/R1 and Trade 003B/R1/C/D/E supersede their old omissions. Keep their source-
backed save timing, concurrent-edit handling, ID composition, history ordering,
loading/error and preview-fencing behavior unchanged.

Current production is Pages.jsx plus previewAutomationContracts.js,
previewAutomationFixtures.js, previewAfkContracts.js and previewConfig.js;
inspect actual imports/callers. Canonical recovered UI/CSS, no redesign/fallback.
Preserve source ordering, defaults, exact labels and even counterintuitive rules.
Retain accepted per-profile drafts, old-ack handling and Retry/Discard behavior.

Lead reran the historical CORRECT-003/check-state.mjs: line 86 expects literal
true for previewTradeFixture('automation-trade-error').error, while current
accepted Trade returns the error string 'Error: Fixture Trade goods request
failed'. This is a stale expectation to investigate using accepted source and
current code, not authorization to change the fixture back to a legacy boolean.
Use a new packet/current replay without rewriting historical results.

## Milestone A — Automation residuals

Make a compact current branch checklist with original locators before edits.
Verify and complete these named residuals only:

1. Secret Task Dispatch Assist allied-task list, selection, manual schedule
   presentation and status/queue/empty/error predicates, including auto-assist
   versus manual controls, delay/quality conditions and selected identity.
2. Resource Gather config-backed radius/resume/recall and per-squad choices,
   view-change retention and recovered runtime-step presentation.
3. Remaining Automation summary/result/error/timing/shield rows already added
   in CORRECT-003: Railway/Secret Task capability/continuation, alliance-task
   timings/counters and Shield window/end/pending-reason states. Inventory the
   actual cards; do not hide a known branch behind a permanent empty state.

Compare actual original renderers/handlers with current production for critical
defaults, status precedence, enable/disable rules and transitions. Source-marker
checks or fixtures matching their own constants are insufficient. Browser-check
representative current controls/statuses in English/light and Japanese/dark.
Fix only confirmed assigned-scope discrepancies; preserve accepted weekly/Trade.
Commit and push a coherent A checkpoint, then continue B.

## Milestone B — AFK residuals

Verify and complete target grouping/options/undiscovered cases, custom/list
restoration, searchable attack-range warning and filter restrictions; join-member
loading/failed/offline/left/self/missing-target variants and selection confirmation;
Potion/Master, Alliance Drill, Garrison and Zombie Bus forms/runtime/conditional
states. Use current code before deciding a historical gap still exists.

Test two distinct profile IDs and new-profile defaults, local draft edits,
switching/navigation round trips, error/Retry/Discard where relevant, modal
selection/confirmation/cancel/Escape and source-required ordering. Recover actual
DOM/handlers and payload-normalization shape where locally supplied. Keep runtime
data synthetic only in disclosed preview cases, not presented as real game state.

Audit explicit preview fixtures versus default/native/native-unavailable mode.
Recoverable presentation must exist, but hardcoded positive lists/counters must
not leak into inactive modes. Use inert supplied state for controlled tests and
actual mode predicates. Do not add success-producing native providers or services.

Do not edit the pre-existing dirty previewAfkFixtures.js. If new fixture coverage
is necessary, add a task-owned fixture module/adapter that leaves its bytes
unchanged. Other pre-existing WIP remains protected. Equipment is not a new
campaign; correct only a demonstrated regression caused by this assignment.

## Verification, evidence and boundary

Allowed: directly needed Pages/contracts/local-state code, isolated preview
adapters, focused source/render/handler/state tests, browser QA and current docs.
No Last War launch/control, native scheduling/gameplay, new provider family,
original auth bypass, login/account/licensing UI, original protected-runtime
pixels, other-page redesign or broad project cleanup.

Create evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/ with
exact source/locator manifest, fixed branch matrix, immutable failing baseline
for fixes, actual original/current cases, browser records and inspected meaningful
screenshots, protected hashes and executable evidence validator. Preserve all
old packets. Write a dated review and conservative current docs/handoff updates.
Mark source/local proof separately from native, actual game assets and pixels.
Do not self-declare the full UI or all eight pages accepted.

Run focused changed/unreviewed branch checks and accepted weekly/Trade/config
regressions when affected; retain Map/Home integration gates. Run canonical
npm.cmd run check, build, check:production-build from src/LWBridge.UI-0.3.17,
the new validator/protected guard and diff/staged checks. Avoid unnecessary
repeats of expensive unaffected Map suites. Self-review the complete diff.

Preserve byte-for-byte and unstaged all seven paths pinned by
MAP-AUTO-CONFIG-001/protected-wip-before.json, including both old filter result
files, previewAfkFixtures.js, .scratch-lwb317/ and CORRECT-003/screenshots/.
Inventory additional actual WIP too. No reset, clean, stash, broad stage or
force-push. Clean up only task-owned browser/process/storage resources.

Continue A and B until acceptance checks pass or a concrete blocker is recorded;
no fixed elapsed-time stop. Commit/push coherent milestones and verify direct
remote SHA. If interrupted, record exact next case/command, completed branches
and remaining work. Return AWAITING_REVIEW with facts, corrections, valid/invalid
findings, results, screenshots/evidence/review paths, exact commit/remote SHA,
protected-WIP status and a finite list of genuine remaining UI gaps. Stop here;
the project lead assigns the subsequent four-page/final-integration check.
