# LWB317-UI-CORRECT-003E — Trade status/loading/error presentation only

Project-lead assignment: 2026-10-02. State: READY.
One remaining presentation unit within Trade Station, not broad CORRECT-003.

## Fresh-chat context

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Reviewed baseline:
bd80899491d24a7d9a081830fc844937fe976d10.
Start at the newest pushed lead checkpoint containing this assignment. Read
AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this assignment and
docs/reviews/2026-10-02-LWB317-PM-014-trade-switch-acceptance.md.
Weekly settings and Trade selection/history/cross-server setting are accepted for
source/local scope. Preserve them. Full native/original parity remains unaccepted.

Reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Exact source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/
AutomationPanel-BJ0gIqFh.js, SHA-256
6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Inspect only the Trade pe({profileId,online,config,status}) renderer/effect and
relevant locale keys. Record exact byte/handler locators for recovered facts.

Clone inputs: TradeStationCard in src/LWBridge.UI-0.3.17/src/Pages.jsx and
previewTradeFixture in previewAutomationFixtures.js. Reuse existing code/fixtures.

## Complete only these displays

1. Recover the four stats: detectedCount, attemptedCount, succeededCount and
   lastResult.state. Verify zero/absent defaults and the exact translation key or
   '-' for last result. Use recovered field names and result translation keys;
   numerical fixtures are disclosed QA values, not actual-game counts or formulas.
2. Recover loading/no-goods predicates and goods-fetch error display. The source
   displays String(error); clone currently renders common.actionFailed for a
   boolean fixture error. Fix demonstrated differences only. Goods loading or
   fetch failure must not be assumed to erase independent purchase-status/history
   data. Include source-supported retained goods while loading/error if recovered.
3. Keep config-save failure, goods-fetch error and purchase last-result state
   distinct. Do not add new retry buttons, timing, sorting, fetch services or
   status rules absent from the source. Accepted Retry/Discard stays unchanged.
4. Add only focused browser-preview QA states where existing fixtures cannot
   represent a recovered branch. Keep accepted positive/selection/history fixture
   behavior stable. Exercise absent status, loading, empty goods, fetch error text
   and recovered last-result states. Fixtures remain synthetic and local-only.
5. Check actual production render expressions/helper outputs against source;
   browser-check loading/empty/error plus representative stats/results. Capture
   at most two useful new images, loading and fetch error. No all-page campaign.

## Delivery

Write docs/reviews/2026-10-02-LWB317-UI-CORRECT-003E.md and focused evidence under
evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003E/: hashes/locators, state/render
checks, actual browser actions/results, screenshot hashes and remaining limits.
Update relevant current UI/status/handoff coverage conservatively; do not declare
the whole Trade implementation or Automation complete.

Run focused checks and existing Trade selection/composition/history/cross-server
checks, canonical npm.cmd run check/build/check:production-build, evidence JSON/
source/image validation and git diff --check. Review, commit/push only this unit
to origin/research/offline-controller and verify remote identity. Return
AWAITING_REVIEW with commit/checks/evidence and preserved WIP, then stop. No fixed
time limit; finish these criteria or report a concrete blocker/continuation point.

## Boundaries

No purchase execution, cross-server gameplay, template/status native providers,
other Automation cards, Assist, AFK, Map, accepted-control/history refactors,
Last War launch/control, service/auth bypass, login/licensing UI, fallback or
subagents. Keep exact CSS/reference assets unchanged. No native fixture fallback.
Inspect existing preview tabs/processes; close only those owned by this task.

Preserve unchanged and unstaged: src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js,
.scratch-lwb317/ and evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/.
Never reset, clean, force-push or discard unrelated work.
