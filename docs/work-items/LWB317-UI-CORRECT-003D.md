# LWB317-UI-CORRECT-003D — Trade cross-server setting only

Project-lead assignment: 2026-10-02. State: COMPLETE / ACCEPTED for focused local scope after PM-014.
One switch and its local save behavior. Do not resume broad CORRECT-003.

Worker bd808994 passed independent actual-callback/regression/package/evidence
checks. PM-014 accepts the local/source setting UI, not cross-server gameplay.
This unit is closed; the next separate task is LWB317-UI-CORRECT-003E.md.

## Fresh-chat context

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Reviewed history baseline:
1a25a9c92aeb03f7239f3066ba9a31e6b9be0116.
Start at the latest pushed lead checkpoint containing this assignment; preserve
newer work. Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this assignment and
docs/reviews/2026-10-02-LWB317-PM-013-trade-history-acceptance.md.
Weekly settings, Trade selections and Purchased-items presentation are accepted
for source/local scope. Preserve them; the full clone remains incomplete.

Reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/
AutomationPanel-BJ0gIqFh.js, SHA-256
6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Trade cross-server label starts at UTF-8 byte 8264; defaults at 400 and immediate
save helper at 7213. Verify anchors yourself. Original switch passes disabled:!r;
clone TradeStationCard currently disables it for !previewEnabled || config.saving.

## Complete only this switch

1. Recover its label/default, disabled predicate and immediate crossServerEnabled
   write contract. Fix the demonstrated save-time lock mismatch only; keep the
   offline restriction. Do not change generic ToggleRow or shared config behavior.
2. Check on/off updates preserve selected currencies, goods and enabled state.
   Use actual production onChange/save callbacks with the real draft store and a
   deferred adapter: a second toggle must be permitted during the first write,
   queued correctly and preserved when the older acknowledgement arrives.
   Include actual Retry and Discard callbacks after failures. Do not add saves in
   the test that the production callback itself does not perform.
3. Focused browser QA using existing Trade fixtures: toggle on/off and inspect
   retained goods/currencies; failed save with Retry/Discard; default offline UI
   keeps the switch disabled. No gameplay action or cross-server operation.
   Record actual results; no new screenshot campaign or fixture overhaul.

## Evidence and completion

Write docs/reviews/2026-10-02-LWB317-UI-CORRECT-003D.md and focused evidence under
evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003D/: source hashes/locators,
actual callback/deferred/recovery checks, browser results and verification.
Update only relevant UI/status/handoff continuation notes conservatively.

Run focused switch checks, existing Trade selection/composition/history checks,
canonical npm.cmd run check/build/check:production-build, source/evidence JSON
validation and git diff --check. Review, commit/push only this unit to
origin/research/offline-controller; verify remote identity. Return AWAITING_REVIEW
with commit/checks/evidence and preserved WIP, then stop. No fixed time limit;
finish the criteria or document a concrete blocker and continuation point.

## Boundaries

No cross-server/gameplay implementation, purchase execution, runtime/loading/error
overhaul, Assist, AFK, Map, weekly/selection/history refactor, native/service work,
Last War launch/control, auth bypass, login/licensing UI, fallback or subagents.
Keep exact CSS/reference assets unchanged. QA stays local-only; no native fallback.
Inspect existing preview tabs/processes; close only task-owned ones.

Preserve unchanged and unstaged: src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js,
.scratch-lwb317/ and evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/.
Never reset, clean, force-push or discard unrelated work.
