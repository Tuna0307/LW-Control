# LWB317-UI-COMPLETE-001 — complete recoverable 0.3.17 UI/UX across all pages

Project-lead assignment: 2026-10-01. State: CHANGES_REQUIRED.
Lead review: `docs/reviews/2026-10-01-LWB317-PM-006-ui-completion-lead-review.md`.
Continue through `docs/work-items/LWB317-UI-CORRECT-001.md`; the worker checkpoint
is retained as partial implementation, not accepted full source-backed completion.
Supersedes the Home-only assignment `LWB317-UI-HOME-STATES-001`.

## Goal

Complete the source-backed in-scope LWBridge 0.3.17 UI/UX across the canonical
frontend, with Home and Map first. This includes the shell and all eight pages,
their recoverable nested surfaces and state-dependent presentations. Existing
static baseline acceptance does not mean those surfaces are already complete.
No fixed elapsed-time stop applies. Preserve coherent milestone checkpoints.

## Baseline and inputs

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Remote: `https://github.com/Tuna0307/LW-Control.git`.
Remote branch: `origin/research/offline-controller`.
The dispatch prompt supplies the exact expected clean starting HEAD. Verify
local/remote identity before editing and preserve unexpected changes.

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Read `AGENTS.md`, `task.md`, `docs/README.md`, `docs/PROJECT_LEAD.md`,
`docs/AI_WORK_PROTOCOL.md`, `docs/strict-parity-recovery.md`,
`docs/lwbridge-ui.md`, `docs/lwbridge-parity-matrix.md`,
`docs/lwbridge-feature-ledger.md`, `docs/PROJECT_STRUCTURE.md`,
`docs/implementation-handoff.md`, and this assignment.
Read the PM-003 takeover and PM-004/PM-005 owner-clarification reviews dated
2026-10-01. Use the UI-001A package review, UI-002A through UI-002H inventories,
UI-003 shared visual system and UI-007 comparison review dated 2026-09-29 as
locators; inspect exact recovered assets under
`evidence/lwbridge-0.3.17/ui/frontend-package/` to establish each new fact.

## Work order and coverage

1. Audit the current clone against recovered components/locales/CSS and create
   a per-page state/interaction coverage matrix with source locators and gaps.
   Distinguish default page render from complete state coverage.
2. Complete Home first: source-proven folder/setup controls, launch/close/repair
   presentation, busy/status precedence, recovery/error variants and switches.
3. Complete Map UI gaps next: Manual/Auto surfaces, all recovered category tabs,
   controls, filters, table columns/cells, actions, selection/sort/pagination,
   dialogs/popovers, empty/loading/error/connected and scheduled-plunder displays.
   Preserve the working production Map bridge and commands. Do not replace live
   data with fixtures or start another scan/provider-recovery campaign.
4. Complete the remaining primary pages: Automation, Squads / AFK, City Layout,
   Hotkeys, Mini Games and Settings. Cover recovered nested tabs/cards, forms,
   dialogs, validation and data-dependent rendering, not merely their default
   empty/disconnected page. Inspect every relevant recovered component.
5. Audit/fix shared shell, navigation, responsive rules, themes, source-proven
   language behavior and local UI interactions. Preserve exact labels/defaults,
   ordering, icons, spacing and class hierarchy. Do not add dormant surfaces as
   visible navigation without source support.
6. Use isolated preview/test state providers for runtime-dependent branches.
   Demonstrate that fixture data cannot replace native production data.
   Production controls without validated providers retain honest recovered
   unavailable/disabled/error behavior. Never fabricate native success.
7. Verify coverage across pages/states at repeatable desktop and responsive
   viewports, light/dark themes and relevant locale/state branches. Record
   meaningful DOM/interaction checks, screenshots and source comparisons.
8. If legitimate post-auth reference access exists, observe the exact reference
   UI and capture matching states without triggering gameplay. Otherwise keep
   direct original screenshot/pixel parity `BLOCKED`, finish all available
   source-backed work and identify the precise missing evidence.

## Scope boundaries

- No login/account/purchase/subscription/licensing UI in the clone.
- Auth-related local state/contracts may be traced when required for the assigned
  UI predicate, using supplied artifacts and authorized access. Do not stop solely
  because a dependency touches auth. No original access-control bypass, others'
  credentials, or unrelated service/protocol research.
- One canonical production implementation. No new fallback or alternate product
  frontend. Preserve historical source/evidence. Retirement of the existing
  selectable legacy host path is a separate pending host task.
- UI-only campaign: no new gameplay/backend family, game launch/control, live
  scan/server jump, attack/march/gather/plunder/claim/share, native lifecycle
  integration, or original commercial account system.
- Preserve existing Map implementation/contracts/evidence and native bootstrap.
  Fix source-proven UI deviations without speculative native/provider changes.
- No deletion/moving of history, hash-check weakening, force push or discarded WIP.
- One manually dispatched worker; no subagents or independent task dispatch.

## Required outputs and acceptance

Review: `docs/reviews/2026-10-01-LWB317-UI-COMPLETE-001.md`.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-COMPLETE-001/`.
Update the UI master, all affected matrix/ledger rows, project status and handoff.

For every page/surface, report source identity/locator, recovered predicates,
implemented states/interactions, QA evidence, and remaining unknown/blocked
dependencies. Unsupported rows stay explicit. A page screenshot, passing build
or route presence is not full UX parity. Preview rows do not prove live behavior.

Run `npm.cmd ci --prefix src/LWBridge.UI-0.3.17 --no-audit --no-fund` if needed,
then the canonical `check`, `build` and `check:production-build` scripts.
Run meaningful branch/interaction QA and `git diff --check`. Check native Map
adapter regression if shared UI code changes; run Map317 deterministic checks
when the Map adapter/contract changes. Broaden checks only for affected behavior.

Review/check/commit/push coherent page or shared-system milestones to
`origin/research/offline-controller`; verify remote equality after each and
preserve a clear continuation. Continue the assigned recoverable UI work until
covered; no arbitrary clock deadline. On a concrete blocker, document exactly
what is missing and continue independent page work where possible.

Final report: work item/status, full per-page coverage, sources/locators,
files/evidence, exact checks/results, remaining gaps with reasons, checkpoint
SHAs, final local/remote equality, clean/dirty state, cleanup and next action.
Return to the project lead for independent review. Do not open backend recovery
or call the entire one-for-one product complete from this UI campaign.
