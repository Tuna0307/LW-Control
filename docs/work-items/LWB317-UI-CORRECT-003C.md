# LWB317-UI-CORRECT-003C — Trade purchase-history presentation only

Project-lead assignment: 2026-10-01. State: COMPLETE / ACCEPTED for focused local scope after PM-013.
One panel under partial CORRECT-003; do not resume the broad campaign.

Worker 1a25a9c passed independent source/helper/regression/package/evidence checks.
PM-013 accepts local history presentation with declared native-image placeholders;
full original/native parity and purchase execution remain unaccepted. This task
is closed. Continue only under the separate LWB317-UI-CORRECT-003D.md assignment.

## Fresh-chat context

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Accepted selection baseline:
bfc652fd9586f64bbb9c4c221af065e44ad337e3.
Start at the latest pushed lead checkpoint containing this assignment; preserve
all newer work. Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this assignment,
docs/reviews/2026-10-01-LWB317-PM-012-trade-selection-acceptance.md and relevant
current UI notes. Weekly controls and Trade selections are accepted for local
scope; preserve them. The full clone is not complete.

Reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Exact frontend source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/
AutomationPanel-BJ0gIqFh.js, SHA-256
6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Inspect the de({language,gameTexts,purchases}) renderer, its C name resolver and
Purchased items tab caller. Pin their own exact UTF-8 byte locators.

Existing clone inputs: TradeStationCard in src/LWBridge.UI-0.3.17/src/Pages.jsx;
previewTradePurchases/previewTradeFixture in previewAutomationFixtures.js.
Reuse existing implementation. Do not build purchase execution or providers.

## Complete only this panel

1. Recover and verify history tab count/activation, empty message, input ordering,
   day grouping, daily total quantity, repeated-item summary and summary order.
   Follow the original algorithm, including adjacent-day grouping after reversing
   input; do not add chronological sorting or globally merge days by preference.
2. Match source behavior for explicit serverDayStartAt and absent day timestamps,
   dailyPurchaseIndex null/absence, confirmedAfterTimeout visibility and row keys.
   Check item/currency names, quantity, price, time, server, quality/icon markup
   and existing locale formatting. Recover the exact behavior instead of guessing
   default values or converting every day to UTC. Use existing asset placeholders
   if native asset delivery is unimplemented; record that limit.
3. Fix only demonstrated history mismatches. Add focused browser-only QA data for
   multiple days, repeated item IDs and optional fields; keep the existing positive
   selection fixture stable by scoping new data to history QA states. Include an
   empty-history case. Synthetic purchases never represent actual transactions.
4. Verify actual production grouping/render expressions or a production helper
   against source-derived expected results. Cover explicit server-day boundaries,
   repeated-item quantities, preserved row order and missing optional fields.
   Do not test a separately reimplemented clone algorithm and call it proof.
5. Real browser QA: switch Goods to Purchased items, inspect populated multi-day
   totals/rows, verify the empty message and switch back. Check one additional
   locale for recovered number/date formatting. Capture only two useful images:
   populated history and empty history. No all-page or nine-locale campaign.

## Evidence, checks and delivery

Write docs/reviews/2026-10-01-LWB317-UI-CORRECT-003C.md and focused evidence under
evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003C/: source hashes/locators,
actual-production checks, browser actions/results, screenshot hashes and exact
remaining limits. Update relevant UI/status/handoff notes and history coverage
conservatively. Original/native/full UI parity remains unaccepted.

Run focused history checks, existing Trade selection and PM-011 composition
checks, canonical npm.cmd run check/build/check:production-build, evidence JSON/
source/screenshot validation and git diff --check. Review, commit/push only this
unit to origin/research/offline-controller and verify remote identity. Return
AWAITING_REVIEW with commit/evidence/checks and preserved WIP, then stop. No fixed
time limit; finish these criteria or document a concrete blocker/continuation.

## Boundaries

No purchase execution, cross-server toggle behavior, runtime-counter overhaul,
Dispatch Assist, AFK, Map, weekly/selection refactors, native/service work, Last War
launch/control, auth bypass, login/licensing UI, fallback or subagents. Keep exact
CSS/reference assets unchanged. Local fixtures must never become native fallbacks.
Inspect existing preview tabs/processes before use; clean up only task-owned ones.

Preserve unchanged and unstaged: src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js,
.scratch-lwb317/ and evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/.
Do not reset, clean, force-push or discard unrelated work.
