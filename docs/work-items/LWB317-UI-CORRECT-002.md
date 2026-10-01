# LWB317-UI-CORRECT-002 — finish source-backed forms and isolate UI drafts

Project-lead assignment: 2026-10-01. State: AWAITING_REVIEW.
Parent UI-CORRECT-001 is CHANGES_REQUIRED after PM-007; its Home fix is accepted
at the source/transport boundary. Preserve useful corrections and evidence.

2026-10-01 recovery: the owner asked the project lead to finish the interrupted
worker. The lead preserved its uncommitted Pages/Map/test changes and two captures
at baseline `a19905d`, then completed the focused correction and QA checkpoint.
Delivery: `docs/reviews/2026-10-01-LWB317-UI-CORRECT-002.md` and the matching
evidence tree. Independent review remains pending; full eight-page parity is
not claimed. Remaining source-recoverable branches are explicitly inventoried.

## Fresh-chat context

You are one manually dispatched worker with no assumed prior conversation.
The owner's goal is one-for-one LWBridge 0.3.17 post-auth UI/UX across all eight
pages, followed by native functions/current-game mapping. No original login or
commercial account UI is required. No alternate/legacy product fallback is wanted.
Current work is UI correction, not new gameplay integration. This assignment
focuses demonstrated remaining gaps; it does not reduce the overall eight-page goal.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Remote: `https://github.com/Tuna0307/LW-Control.git`.
Reviewed worker baseline: `543b6ebffb62125cd5e1c5d290260c5ad8445aff`.
Start from the latest pushed project-lead checkpoint containing PM-007 and this
assignment. Verify branch, WIP and local/remote identity; preserve newer/unrelated
changes. Never reset or force-push.

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Canonical UI: `src/LWBridge.UI-0.3.17`.
Exact assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.

Read AGENTS.md first, then task.md, docs/AI_WORK_PROTOCOL.md,
docs/PROJECT_LEAD.md, docs/strict-parity-recovery.md,
docs/reviews/2026-10-01-LWB317-PM-007-ui-correction-lead-review.md,
this assignment, docs/lwbridge-ui.md, docs/lwbridge-parity-matrix.md,
docs/lwbridge-feature-ledger.md, docs/lwbridge-project-status.md and
docs/implementation-handoff.md. Read UI-CORRECT-001's review/matrix/QA/manifest
as worker evidence, and its lead-review/browser-findings.json for reproductions.
PM-006 supplies historical findings; PM-007 is the current disposition.

## Work

1. Establish per-control contracts for the affected Automation and AFK components
   before coding. Include exact source hash, UTF-8 byte/render/handler locator,
   data producer/consumer, options, conditional branch, value/default/limit,
   validation and state transition. Trace imported helpers/data composition when
   needed. Distinguish recoverable rules from synthetic fixture values. Do not
   insert a generic field or label-only stand-in for an unimplemented branch.

2. Finish Construction's category tabs, keyboard navigation, per-building choices,
   selected summary and validation. Implement Soldier Training's available-level,
   quantity/order/status/camp branches. Restore Resource Gather's actual radius,
   delay choices and data-dependent squad cards instead of constant loading.
   Recover Train fixed selection/clear/Driver and reward-preference selection/order
   branches. Audit the remaining Automation card structures and local config
   draft/error/validation behavior so these examples are not simply patched in
   isolation. Preserve exact defaults/limits and card-specific state rules.

3. Give each AFK profile a complete independent local draft using the recovered
   field contract, including target, assignments, execution/filter values and
   join/member state. Switching or adding profiles must load the correct draft
   and preserve prior edits according to the recovered workflow. Do not solve
   leakage by resetting every editor on selection. Derive profile summaries from
   their actual drafts. Implement source-proven validation/unsaved/save-state
   behavior in the isolated preview provider; native writes remain unavailable.

4. Make the existing read-only Map fixture useful for actual UI assertions.
   Honor the supported query filters, sort, page and pageSize over a disclosed
   deterministic dataset; compute consistent totals/pages. Include zero matches
   and last-page cases. Match the existing recovered Map query contract; do not
   create guessed production fields or change the production provider. Prove
   native/native-unavailable cannot select the fixture and that game/mutation
   operations still reject. Preview fixtures are a QA tool, not a product fallback.

5. Verify real local transitions, not just marker strings. At minimum: Construction
   category keyboard/checkbox/summary/empty validation; Training valid/invalid
   quantity and available-level/state branches; Gather option/squad drafts; Train
   clear-selection/reward ordering; two AFK profiles with different target/squad/
   join edits and round trips; new-profile independence; Map applied filters/sort,
   page-size/final-page counts and zero result. Preserve source DOM/CSS hierarchy,
   labels and keyboard behavior. Check corrected UI in light/dark, relevant
   locales and desktop/responsive views. Disclose harness versus physical input.

6. Reconcile affected coverage rows. Every unimplemented source-recoverable branch
   remains an implementation gap, not a native-provider or original-auth blocker.
   List unreviewed branches explicitly. Do not claim the whole product/all eight
   pages complete from this focused correction. Return to lead review.

## Boundaries

UI/local preview and read-only offline QA only. No game launch/control, live scan,
server jump, gameplay action, native lifecycle/provider family, updater install,
external submission or original protected-service access. Do not modify backend
scope validation or production Map contracts to make tests pass. Preserve accepted
Home correction, historical evidence and exact CSS/assets/hash checks. No subagents
or separate worker dispatch. Local UI dependency tracing follows AGENTS section 6;
no original access-control bypass or login/account/licensing UI reconstruction.

## Delivery and acceptance

Review: `docs/reviews/2026-10-01-LWB317-UI-CORRECT-002.md`.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-002/`.
Deliver per-control source contracts, complete affected branch matrix, meaningful
interaction/negative checks, repeatable screenshots and exact remaining gaps.
Update current UI master/matrix/ledger/status/handoff conservatively. Preserve
PM-007 and prior worker reports; only the project lead accepts completion.

Run canonical check/build/check:production-build, targeted draft/handler/DOM tests,
Map deterministic checks if production-shared Map behavior changes, and git
diff --check. Passing English-key/marker checks is not branch proof. Check actual
callback/state outcomes. Confirm source/screenshot hashes and preview fencing.

No fixed 20-minute stop. Continue to acceptance or a concrete blocker; finish
independent work if one dependency is blocked. Review/commit/push coherent
milestones to origin/research/offline-controller and verify remote equality.
Clean up only owned QA tabs/processes. Return AWAITING_REVIEW with C1–C3
resolution, source locators, exact checks/limits, remaining gaps, files/evidence,
commit SHAs, local/remote identity, clean/dirty state and exact continuation.
