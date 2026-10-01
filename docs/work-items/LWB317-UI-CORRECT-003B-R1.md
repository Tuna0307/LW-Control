# LWB317-UI-CORRECT-003B-R1 — currency composition only

Project-lead assignment: 2026-10-01. State: COMPLETE / ACCEPTED after PM-012.
One small correction to CORRECT-003B. Do not resume parent CORRECT-003.

R1 bfc652f passed independent composition/selection/package/evidence checks.
PM-012 accepts the focused local selection unit. This assignment is closed;
the next separate task is LWB317-UI-CORRECT-003C.md, purchase history only.

## Context for a fresh worker

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Reviewed worker baseline:
a24bf6c736398e76c2e01451e6761e4d96fd28ec.
Start at the newest pushed lead checkpoint containing this assignment. Preserve
all newer changes. Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this file and
docs/reviews/2026-10-01-LWB317-PM-011-trade-selection-review.md.

The save-time selector fix passes lead review. Weekly work is accepted under
PM-010. Preserve both. This task fixes only two existing TradeStationCard currency
composition expressions in src/LWBridge.UI-0.3.17/src/Pages.jsx.

Reference executable: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Exact source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/
AutomationPanel-BJ0gIqFh.js.
SHA-256: 6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.

## Required corrections

1. currencies: retain the first encountered offer for each positive currencyId,
   then sort currency options by ascending ID. Current Map construction keeps the
   last offer instead. Original first-wins loop starts at UTF-8 byte 5605.
2. possibleCurrencies: deduplicate offers by currencyId, retain the first label
   for each ID in encounter order, then render those labels. Current name-based
   Set is wrong. Original ID-based loop starts at UTF-8 byte 1197.

Use the existing fixture data shape and existing normalized currencyName values.
No new translation/provider architecture or broad helper refactor. Keep the
actual-production-expression harness runnable; if extraction needs adjustment
because of the correction, preserve its source oracle and all three scenarios.

## Acceptance and evidence

- The lead check at evidence/lwbridge-0.3.17/ui/LWB317-PM-011/
  check-trade-composition.mjs must pass all three cases. Preserve its failing
  composition-baseline.json; save the passing output under the R1 evidence tree.
- Rerun the existing CORRECT-003B/check-trade-selection.mjs to prove selection,
  immediate/concurrent saves, last-currency guard and Retry/Discard still pass.
- Perform one focused browser recheck of the existing positive Trade preview:
  currency/goods labels, toggling, Show exclusive and last-currency protection.
  Record actual actions/results; reuse unchanged existing screenshots with their
  hashes where appropriate. No new all-page/locale/screenshot campaign.
- Run canonical npm.cmd run check, build, check:production-build and git diff
  --check. Verify original source hash/anchors and evidence JSON.
- Write docs/reviews/2026-10-01-LWB317-UI-CORRECT-003B-R1.md and focused evidence
  under evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003B-R1/. Update only relevant
  UI/status/handoff continuation notes conservatively; full parity is unaccepted.
- Review, commit and push only this unit to origin/research/offline-controller;
  verify remote identity. Return AWAITING_REVIEW with exact commit/checks/evidence
  and preserved WIP, then stop. Continue until these criteria pass or a concrete
  blocker is documented; no fixed time limit.

## Boundaries

No purchase history, cross-server toggle, runtime status/counters, Dispatch Assist,
AFK, Map, weekly changes, native/service integration, Last War launch/control,
auth bypass, login/licensing UI, fallback, or subagents. Keep exact CSS/assets
unchanged. QA metadata must remain local preview/test data, never a native fallback.
Inspect existing browser tabs/processes before use; close only task-owned ones.

Preserve unchanged and unstaged: src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js,
.scratch-lwb317/ and evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/.
Never reset, clean, force-push or discard unrelated work.
