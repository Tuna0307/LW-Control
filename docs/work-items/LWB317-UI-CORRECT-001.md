# LWB317-UI-CORRECT-001 — repair and verify full recoverable UI coverage

Project-lead assignment: 2026-10-01. State: CHANGES_REQUIRED.
PM-007 review accepts the Home request-scope fix and retains useful corrections;
remaining Automation/AFK implementation and Map QA gaps continue through
`docs/work-items/LWB317-UI-CORRECT-002.md`.
Parent: `LWB317-UI-COMPLETE-001`, returned **CHANGES_REQUIRED** by PM-006.

## Fresh-chat context and goal

You are one worker AI. The owner manually dispatches fresh chats, so do not
assume any previous conversation or memory. Complete this assignment in the
existing canonical implementation and return evidence to the project lead.

Owner goal: a one-for-one reproduction of LWBridge 0.3.17's in-scope post-auth
experience. No login/account/licensing page or commercial account system is
required. Current priority is complete UI/UX before new native game functions.
Home and Map come first, and all eight pages plus shared/nested surfaces remain
in scope. No fixed twenty-minute block or clock-based stop applies. Commit
coherent milestones and continue until acceptance criteria are met or a concrete
documented blocker prevents the dependent work.

The previous worker added useful states/translations/screenshots, but the lead
found a native preference request defect, incorrect generic Automation forms,
missing Equipment dialogs/interactions and insufficient branch coverage. Do not
replay the task from scratch, discard those changes, or treat its broad completion
claims as accepted. Direct original screenshot/pixel comparison is separately
blocked by missing legitimate matched post-auth reference access. That does not
prevent repairing source-proven implementation gaps.

## Repository and exact reference

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Remote: `https://github.com/Tuna0307/LW-Control.git`.
Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Reviewed worker baseline: `a253cceff5f06512eeedaaf33cf6617f1e6170b3`.
Start from the latest pushed lead checkpoint containing this assignment and
PM-006. Verify branch/status and local/remote identity; preserve unexpected WIP
and reconcile newer relevant changes before editing. Do not reset or force-push.

Canonical frontend: `src/LWBridge.UI-0.3.17`.
Production Desktop package: `src/LWBridge.Desktop/ProductionUi`.
Exact recovered assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets`.
Prior worker evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-COMPLETE-001`.

Read before implementation:

1. `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`, `docs/PROJECT_LEAD.md`.
2. `docs/reviews/2026-10-01-LWB317-PM-006-ui-completion-lead-review.md`.
3. This assignment and `docs/work-items/LWB317-UI-COMPLETE-001.md`.
4. `docs/strict-parity-recovery.md`, `docs/lwbridge-ui.md`,
   `docs/lwbridge-parity-matrix.md`, `docs/lwbridge-feature-ledger.md`,
   `docs/lwbridge-project-status.md`, `docs/implementation-handoff.md`,
   `docs/PROJECT_STRUCTURE.md` and `docs/README.md`.
5. The previous worker review and coverage/QA/source manifest. Use UI-002A
   through UI-002H and UI-003/UI-007 as navigation aids; establish new facts from
   the exact recovered 0.3.17 bytes, not from an older summary.

## Work and allowed scope

1. Create a granular all-page audit before claiming completion. Inventory every
   recoverable nested tab, card, form, dialog, popover and relevant conditional
   branch, including empty/loading/error/busy/connected/populated states.
   Each row needs source hash + zero-based UTF-8 byte/expression locator,
   predicate/defaults/limits, clone location, QA and exact open dependency.
   A filename, translated label, preview marker or default screenshot alone
   cannot establish a complete UI contract.

2. Repair Home's existing Automatic Reconnection request. The current callback
   sends `set_automation` without `profileId`, while the host requires it before
   dispatch. Use the selected-profile bootstrap contract; keep host validation
   intact. Cover missing profile, failure, busy/disabled and acknowledged-success
   behavior with isolated transport tests. This narrowly authorizes correction
   of the existing UI preference call, not new native lifecycle or backend work.
   Audit the other Home predicates/status/recovery branches against Gr/Kr and
   their consumers; do not accept them solely from preview marker strings.

3. Audit Map next against `MapDataPanel-B4GXEND2.js`: all recovered Manual/Auto
   tabs, filters/columns/cells/actions, selection/sorting/pagination, loading/error
   and nested dialogs/popovers/scheduled-state presentation. Preserve the existing
   production data/bridge contract and independently recorded live proofs.
   Do not launch a live scan or start provider recovery. Positive-row UI branches
   may be exercised in an isolated offline test harness; no fixture may replace
   native production Map rows or become a product fallback.

4. Replace generic Automation substitutes with each card's exact recovered
   structure. At minimum fix Ghost own-start/alliance-join/filter/claim branches,
   Secret Task execution/reset-delay/quality branches, Construction auto-claim
   and card-specific collapsibility. Audit every category/card, status/action
   row, form validation, config save/error/unsaved state and local transition.
   Do not invent a shared three-minute quality form or a sixty-minute control for
   unhandled cards. Backend execution may remain unavailable; recoverable local
   UI behavior must still work in the preview/test provider.

5. Complete Equipment's recoverable UI. Implement the Rename dialog, input,
   Enter/Cancel/Save, empty-name/busy predicates, create/select/edit state and
   recovered drag/drop/result/progress presentation where exact source supports
   them. Inspect the underlying handlers and item renderer. Enabled local preview
   buttons must produce the expected local transition. Native read/save/apply
   operations without validated providers stay honestly unavailable; never
   fabricate a native success. Audit AFK profiles, drill/garrison/zombie-bus and
   their nested surfaces as well.

6. Audit and correct City Layout, Hotkeys, Mini Games, Settings and shared shell
   with the same granularity. Do not infer they are complete because the lead
   review sampled other pages. Cover source-recoverable validation, error/result,
   keyboard/focus, local transitions and conditional presentations. Resolve
   inherited in-scope locale messages from the original module composition,
   including updater errors; do not rebuild excluded auth-error UI. Preserve
   exact labels, layout/classes, icons, ordering, defaults and responsive rules.

7. Use explicitly fenced preview/test providers for runtime-dependent branches.
   Exercise real local handlers, not static pictures or generic form stand-ins.
   Distinguish synthetic fixture values from recovered rules and native results.
   Prove a native bootstrap cannot select fixtures via preview query parameters.
   No additional product frontend, alternate fallback, or redesign is authorized.

8. Independently compare each corrected branch with the recovered render
   expressions/CSS. Capture repeatable desktop/responsive and light/dark/locale
   evidence, and meaningful interaction/negative-state checks. If legitimate
   reference access exists, matched passive original UI capture is permitted;
   do not bypass access controls or trigger game actions. Missing original pixels
   stay `BLOCKED`; finish all independent source-backed work first.

## Scope boundaries

UI correction only. No new gameplay/backend family, native launch/close/repair,
live scan/server jump, attack/march/gather/plunder/claim/share, updater installation,
external feedback submission or protected-service access. Do not control or close
owner sessions. Local dependency tracing needed for UI predicates is allowed
under AGENTS section 6. Login/account/purchase/subscription/licensing UI remains
excluded. Preserve histories and exact assets; do not weaken hash checks or
backend scoping. Retirement of `--legacy-ui` is a separate host assignment.
One manually dispatched worker; no subagents or separate worker dispatch.

## Required outputs and acceptance

Review: `docs/reviews/2026-10-01-LWB317-UI-CORRECT-001.md`.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/`.
Produce a granular coverage matrix, source manifest, interaction/negative-state
QA and screenshots with exact viewports/state/locale. For R1–R4, identify the
fix or remaining precisely sourced blocker. Preserve the previous evidence and
lead review. Update affected UI master/matrix/ledger/status/handoff conservatively;
unimplemented branches are open implementation gaps, not completed rows awaiting
only original screenshot validation.

Acceptance requires all recoverable branches covered or explicitly itemized with
an exact dependency and reason; no generic placeholders presented as parity, no
enabled inert local controls, no fake native success, and no undocumented
unreviewed page assumed complete. Original pixels and later native functionality
retain their separate validation states. Return `AWAITING_REVIEW`; only the lead
accepts completion or opens another function family.

Run canonical `check`, `build`, `check:production-build` under
`src/LWBridge.UI-0.3.17`, meaningful handler/DOM/state checks for corrected
behavior, and `git diff --check`. Run Map deterministic checks if shared Map
behavior/contracts change. A marker-counting checker cannot substitute for
branch/interaction QA. Use existing tools; no elapsed-time cutoff.

Review the diff, commit/push coherent milestones to
`origin/research/offline-controller`, verify the remote revision, and clean up
only owned QA processes/tabs. Do not leave an unreported dirty checkpoint.

Final report: work item/status; R1–R4 resolution; all-page granular coverage;
source locators; checks and their limits; files/evidence; remaining implementation
gaps versus original-visual/native-provider blockers; commit SHAs; local/remote
equality; clean/dirty state; cleanup and exact continuation point.
