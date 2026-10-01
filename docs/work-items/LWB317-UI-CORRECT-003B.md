# LWB317-UI-CORRECT-003B — Trade Station selection controls only

Project-lead assignment: 2026-10-01. State: CHANGES_REQUIRED after PM-011.
One small unit under partial CORRECT-003. Do not resume its entire backlog.

Worker delivery a24bf6c passes saving/selection/recovery checks. PM-011 found two
existing currency composition mismatches: first-offer retention and deduplication
by currency ID. Continue only under LWB317-UI-CORRECT-003B-R1.md. See
../reviews/2026-10-01-LWB317-PM-011-trade-selection-review.md.

## Context and inputs

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Reviewed implementation baseline:
d065d08895088df9f3f053ebe0c78a6e048b6e91.
Start from the newest pushed lead checkpoint containing this assignment; preserve
newer changes and WIP. Never reset/clean/force-push. Weekly controls are accepted
under PM-010 and must not be reopened or refactored in this task.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md and this assignment. Read only
the relevant current UI master and TradeStationCard in Pages.jsx,
previewTradeFixture/previewTradeGoods in previewAutomationFixtures.js, and the
existing shared draft/error store as needed. The implementation already exists
from 5204f67; inspect and reuse it rather than rebuilding the panel.

Reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Exact UI asset: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/
AutomationPanel-BJ0gIqFh.js, SHA-256
6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Trace its Trade render/config helpers only where needed for these controls.

## Finish only this scope

1. Recover exact currency defaults/options, selection rules, goods ordering,
   exclusive-offer visibility/selectability, enable prerequisites and config
   write timing/disabled predicates. Record source hash and byte/handler locators.
2. Verify the existing local preview's currency toggles, last-currency rule,
   goods select/unselect and Show exclusive control against source. Cover positive
   goods, empty goods and exclusive-only goods if those branches are recovered.
   Fix only demonstrated mismatches in those selection controls/data composition.
3. Verify edits update the correct selectedCurrencyIds/selectedItemIds and retain
   source-prescribed state across settings/category/navigation changes. Check
   the source enable/disable rule when selecting or clearing the final good.
4. Exercise failed selection save, Retry and Discard. Check saving/offline/task-busy
   restrictions and write timing from source, rather than copying the weekly rule
   by assumption. Use actual selection callbacks/store with a deferred adapter
   when needed; do not manually add a missing save in a test. Synthetic fixture
   item/currency names and IDs are QA values, not claims about the current game.

## Boundaries

No purchase-history completion, purchase execution, cross-server behavior,
runtime counters/status overhaul, Dispatch Assist, AFK, Map, weekly changes or
other Automation forms. A shared helper may be inspected but must not be broadly
refactored. No Last War launch/control, native/service work, login/licensing UI,
fallback, auth bypass or subagents. CSS/source assets remain unchanged.

Keep uncommitted previewAfkFixtures.js, .scratch-lwb317 and parent screenshots
unchanged and unstaged. Report them rather than clearing WIP. Preview data must
remain local-only and cannot become a native/provider fallback. Inspect existing
QA tabs/processes before using them; clean up only those owned by this task.

## Verification and delivery

Write a short docs/reviews/2026-10-01-LWB317-UI-CORRECT-003B.md and focused
evidence under evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003B/: exact contracts,
actual interaction results, hashes and two useful screenshots (selected goods/
currencies and save-error or exclusive state). No all-page screenshot campaign.
Run canonical npm check/build/check:production-build, focused actual-handler tests
where needed, evidence hash/anchor validation and git diff --check. Do not repeat
unrelated gameplay/runtime testing. Update only the Trade selection coverage and
continuation notes; history and other unreviewed branches remain explicitly open.

Review/commit/push only this unit to origin/research/offline-controller and verify
remote identity. No fixed clock deadline; finish this bounded unit or report a
concrete blocker. Return AWAITING_REVIEW with evidence/checks/commit and unrelated
WIP, then stop. Do not begin another feature automatically.
