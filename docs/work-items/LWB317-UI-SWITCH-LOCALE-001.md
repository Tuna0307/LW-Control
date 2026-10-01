# LWB317-UI-SWITCH-LOCALE-001 — shared switch state descriptions

Owner: returning worker AI. State: COMPLETE / ACCEPTED for focused source/local scope
after project-lead PM-016 review on 2026-10-02; worker delivered 47c243a.
Assigned by project lead at owner's request, 2026-10-02.
Implementation baseline: 1942d51032c2459b3341d54aee186a3fee0ddf2b;
the assignment documentation commit follows it. Use the current checkout; never
reset to the baseline or discard unrelated WIP.

## Goal and scope

Correct only the literal Enabled/Disabled suffix in Pages.jsx ToggleRow aria-label.
Match recovered original Bn with existing common.enabled/common.disabled catalog
keys. Labels supplied to clone ToggleRow are already translated by its callers;
retain those labels and localize only the checked-state suffix. Preserve visible
content, checked/disabled predicates, click callback and optional-callback behavior,
Switch/CSS and all callers. No component-wide redesign or broader locale rewrite.

Allowed edits: ToggleRow localization, focused checker/evidence/review and necessary
conservative delivery documentation. Inspect callers/source read-only to establish
coverage; do not refactor their behavior. If another defect is found, record it
for the lead rather than expanding this task.

## Fresh-chat context and inputs

Repo: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Read AGENTS.md, task.md, AI_WORK_PROTOCOL,
lwbridge-ui.md and the Home presentation queue first; inspect status/current HEAD.

Target: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Recovered asset: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js.
SHA-256: 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Original Bn starts at zero-based UTF-8 byte 213332. It uses translated label plus
common.enabled/common.disabled for its switch aria-label. Verify exact source bytes
and record hash/locator. Do not reconstruct its separate checkbox variant here.

During the worker's break, lead delivered Trade display 003E and Home translation,
separate errors and busy presentation. These remain AWAITING_REVIEW. Do not resume
the broad CORRECT-003 campaign, redo accepted weekly/Trade units, or accept the lead's
implementations yourself. PM-015 is historical baseline evidence; its old audit
asserts defects since corrected and is not a current pass requirement.

Existing unrelated WIP to preserve byte-for-byte and leave unstaged:
- src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js
- .scratch-lwb317/
- evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/

Existing port 4319/PID 62280 preview server was pre-existing at assignment; inspect
ownership before reuse and do not stop an unowned server or close another AI/user tab.

## Acceptance

1. Execute actual original Bn and actual production ToggleRow render/callbacks.
   Compare localized switch descriptions across all nine catalogs, both checked
   states and both disabled states. Preserve role, aria-checked, button type,
   disabled and visible label. Verify an enabled callback gets !checked, disabled
   browser control cannot toggle, and omitted optional callback still behaves as
   the existing clone does. Disclose the already-translated clone-label adapter.
2. Inventory actual ToggleRow callers (Home, Automation/Trade, Squads/AFK, Mini
   Games, Settings) with locators; check representative generated labels and
   preserve their handlers/predicates. This is helper coverage, not a new all-page
   parity campaign. Existing enabled Mini Games or Settings browser-preview control
   must demonstrate off/on/off. Also observe Japanese Home checked/unchecked
   disabled controls using existing home-connected/home-running-disconnected
   fixtures. Capture a useful screenshot and browser results; distinguish local
   preview evidence from native/original-runtime proof.
3. Pass canonical npm.cmd run check, npm.cmd run build and
   npm.cmd run check:production-build in src/LWBridge.UI-0.3.17. Re-run current
   HOME-ERROR-001/check-home-errors.mjs, HOME-ERROR-002/check-home-channels.mjs and
   HOME-BUSY-001/check-home-busy.mjs with --verify-record under their evidence
   directories. Preserve their saved reports. Validate new source/image/JSON
   evidence and git diff --check. Do not weaken existing checks to obtain a pass.

## Delivery boundary

Evidence: evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001/.
Review: docs/reviews/<actual-date>-LWB317-UI-SWITCH-LOCALE-001.md.
Record exact source expressions/locators, caller inventory, focused results,
browser QA/screenshots and verification. Update this item, queue and relevant
current UI/status/matrix/ledger/handoff conservatively to AWAITING_REVIEW; global
UI remains IMPLEMENTED_NOT_VALIDATED. Do not mark the whole queue or prior lead
tasks accepted. Keep the next task unassigned pending lead review.

Review/stage only owned files; commit, push origin/research/offline-controller,
verify local HEAD/tracking ref/direct remote SHA agree. No force-push/reset/clean.
No fixed time block. Complete this one unit, or preserve a coherent checkpoint
with an exact blocker/continuation point. Return work item, status, change, checks,
limits, evidence/review paths, commit and remote verification; stop for lead review.

No native/gameplay control or game launch, original auth/entitlement bypass,
backend campaign, product fallback, separate chats or subagents in this assignment.

## Delivery

Delivered 2026-10-02 for independent project-lead review. `ToggleRow` now uses
`common.enabled` / `common.disabled` only for the aria-label state suffix; its
already-translated caller label, visible content, checked/disabled behavior,
optional callback and all 11 callers are unchanged.

Focused evidence executes recovered `Bn` at UTF-8 byte 213332 and production
`ToggleRow` for 360 render comparisons across all nine languages, ten unique
caller labels and both checked/disabled values. Enabled callback and omitted
optional-callback checks pass. Browser QA passes Settings Show FPS off -> on ->
off, Japanese Home checked/unchecked disabled descriptions, and disabled-click
rejection. Canonical check/build/package and the three current Home regression
checkers pass. Evidence:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001/`.
Review: `docs/reviews/2026-10-02-LWB317-UI-SWITCH-LOCALE-001.md`.

No native/original-runtime proof is claimed. Prior lead deliveries remain
AWAITING_REVIEW, and the next task is unassigned pending project-lead review.

Lead disposition: PM-016 accepted the submitted helper-only change after independent
scope/hash/locator, focused, Home regression and rebuilt package checks passed.
See docs/reviews/2026-10-02-LWB317-PM-016-switch-locale-acceptance.md. Next assignment:
LWB317-REVIEW-HOME-ERROR-001; prior Home deliveries are not yet accepted.
