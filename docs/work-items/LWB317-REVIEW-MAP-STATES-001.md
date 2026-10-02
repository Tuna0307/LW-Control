# LWB317-REVIEW-MAP-STATES-001 — independent review of three Map tables

Date: 2026-10-02. Owner: returning worker. State: ASSIGNED.
Project lead assignment requested by owner: one medium UI-only review.
Implementation: bf84bbdca8a86c1a45e9cbb3bd0170c8fbfcdd7f.
Delivery baseline: 3939ac045ef9c01ac454e06f8a35c72a220cea4b;
assignment documentation follows it. Use current HEAD without reset.

This is the worker's only active assignment. The earlier independent Home busy
review is queued, not cancelled or accepted. Trade 003E and the broader Map table
unit remain pending. Do not execute several reviews in one run.

## Goal and scope

Independently verify the source/local UI of Secret Task, Ghost Ops and Treasure
after the lead's Map table correction. Verify three-table columns/copy, statuses,
selection eligibility, timestamp formatting and supplied-text/unknown behavior.
Focus on distinguishing cases; no exhaustive all-page or all-locale campaign.
This assignment permits review/evidence writes only. Return any defects for a
separate focused correction, rather than altering the implementation under review.

UI/UX means labels, presentation, enabled/disabled states and local interactions
using explicit synthetic data. Successful gameplay or native providers are not
acceptance requirements for this review.

## Read and verify

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/lwbridge-ui.md, this work item and
docs/reviews/2026-10-02-LWB317-UI-LEAD-TABLES-001.md.
Inspect current git status and preserve unrelated work.

Target: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe;
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Original Map source:
evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js;
SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
Inspect actual source bytes/functions independently. The prior results/manifest
provide navigation locators, not substitutes for inspection.

Current production: src/LWBridge.UI-0.3.17/src/mapTablePresentation.js,
src/LWBridge.UI-0.3.17/src/MapDataPage.jsx and mapPreviewApi.js.
Prior evidence: evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/.
Source column factory nt is at UTF-8 byte 9594; locate actual F/P, pe/me and
Treasure resolvers/constants through source and the pinned locator record.

## Independent checks

1. Secret Task/Ghost Ops: pending, ready, protected, full and expired, including
   overlaps/boundaries that distinguish precedence. Inspect seconds/milliseconds
   normalization, absent times/counts and numeric versus invalid UUID selection.
   Verify actual rendered labels/classes and checkbox disabling; do not equate
   local selection with working scheduling.
2. Treasure: charging percentage, known/unknown world states, claimed precedence
   over foreign-alliance/no-scout reasons, failed/unknown player state, type/supplies/
   name resolution with and without supplied game text. Verify the recovered
   refreshing-to-verifying formatter input separately from its producer reachability.
3. Execute a compact independent original-vs-production harness with fixed time,
   using actual original functions and actual current helpers/JSX. Include English
   and Japanese copy. Do not write a second guessed implementation or rely only
   on the prior checker. Record exact source locators and distinguishing results.
4. Browser: use previewPage=map-data, previewState=map-table-states and English;
   navigate Secret Task -> Ghost Ops -> Treasure. Check visible task states and
   disabled selectors, charging/claimed/foreign Treasure labels, then Japanese
   Treasure. Verify row-action controls remain disabled. Capture one or two useful
   screenshots, visually inspect the exact saved files and record console errors.
   Time-relative fixture states may advance; reload for fresh pending/protected
   states and do not claim an earlier expected state after its deadline.

## Checks and delivery

Run prior Map checker --verify-record and validate-evidence.mjs, npm.cmd run check,
and npm.cmd run check:production-build. No product change is authorized, so rebuild
only if package checking establishes a concrete need. Historical parent CORRECT-003
Trade-error boolean assertion is already stale; preserve it and do not investigate
Trade or rewrite historical evidence. Validate your new JSON/source/image records
and git diff --check.

Write docs/reviews/<actual-date>-LWB317-REVIEW-MAP-STATES-001.md and evidence under
evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-STATES-001/; update this assignment's
delivery section only. Leave master acceptance/status integration to the lead.
Return REVIEW_COMPLETE with recommendation ACCEPT or CHANGES_REQUIRED for this
three-table source/local scope. For defects include exact reproduction, expected/
actual values and source/current locators. State remaining UI gaps separately from
native-function and original-pixel limits. Stop after this one review.

Stay on research/offline-controller. Stage explicit owned files, commit, push to
origin/research/offline-controller and verify direct remote SHA. No reset, clean,
force-push or historical record refresh. Preserve unchanged/unstaged:
src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js, .scratch-lwb317/ and parent
CORRECT-003 screenshots. Own/close only your preview tab/server.

No Truck/Train/filter/action/scheduling implementation, Home/Trade review, backend
commands, Last War launch/control, original authentication bypass, fallback or
subagents. No fixed elapsed-time stop: finish this scope or report a concrete
blocker with the exact continuation point.

## Delivery

Date: 2026-10-02. State: **REVIEW_COMPLETE**.
Recommendation: **ACCEPT for the three-table source/local scope**.

Review: `docs/reviews/2026-10-02-LWB317-REVIEW-MAP-STATES-001.md`.
Reproducible evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-STATES-001/`.
No product code or pre-existing evidence was changed by this review. The exact
review commit and verified remote SHA are reported in the reviewer return because
this delivery section is itself part of that commit.
