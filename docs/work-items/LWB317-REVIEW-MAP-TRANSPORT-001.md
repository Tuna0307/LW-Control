# LWB317-REVIEW-MAP-TRANSPORT-001 — independent Truck and Train table review

Date: 2026-10-02. Owner: returning worker. State: ASSIGNED.
This is the worker's only active assignment after PM-021 Home busy acceptance.
Baseline: ff787e31acd048df8091a74da18e9ee3b595eb93; lead integration documentation
follows it. Use current HEAD without reset. Implementation under review:
bf84bbdca8a86c1a45e9cbb3bd0170c8fbfcdd7f.

## Goal and scope

Review only Truck (`truck`) and Train (`railway`) table presentation against exact
LWBridge 0.3.17 source. Independently check columns, values, status/countdown,
eligibility and retained-goods rendering. This is a medium source/local UI review,
not a new implementation or gameplay campaign. Do not edit product code; return
defects for a separate correction.

Accepted Home translation/errors/switch/busy presentation and the three Map
task/Treasure tables remain unchanged. Source/local formatting acceptance does
not establish working gameplay, full UI interactions or original pixel parity.

## Read and verify

Repo: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/lwbridge-ui.md, this assignment and the lead table correction review.
Inspect git status and preserve unrelated changes.

Target: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe.
SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Original assets under evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/:

- MapDataPanel-B4GXEND2.js, SHA-256
  CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089;
  column factory nt at zero-based UTF-8 byte 9594. Locate actual table/select/reward
  renderers independently using the original source and pinned prior locators.
- index-BVfnK1wp.js, SHA-256
  44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6;
  Truck maximum Fe at byte 201275 and Truck state Ie at byte 201399.
- rewardDisplay-eZWrd6iS.js, SHA-256
  7F65DD3F5B81C96117AF5E83E8310D6C6A3A48787B1F693D387020255C78E73E;
  compact-count formatter begins at byte 0.

Production: src/LWBridge.UI-0.3.17/src/mapTablePresentation.js, MapDataPage.jsx
and mapPreviewApi.js. Prior evidence: evidence/lwbridge-0.3.17/ui/
LWB317-UI-LEAD-TABLES-001/, especially map-table-results.json and coverage-matrix.md.
Those locators aid navigation; do not substitute the author's tests for review.

## Required independent checks

1. With fixed time, execute actual recovered and current functions/JSX over a
   compact set of distinguishing Truck cases: invalid IDs/server, expiry and
   protection boundaries, full-versus-protected precedence, special/Reindeer cap,
   normal/missing/invalid counts and eligibility. Check labels and disablement
   separately; do not infer a row's text from its selection eligibility.
2. Compare Truck/Train column order, widths, copy, Live Target presentation,
   owner/alliance fallbacks, quality, power and arrival/protection/updated times.
   Verify Train's own layout rather than assuming it inherits Truck selection.
3. Check retained goods through actual original/current renderers: supplied-name
   fallback, empty goods, reward order with/without selected item, exact counts,
   compact-number boundaries, descriptions/aria labels and conditional itemCount
   sorting metadata. Execute the existing filter/sort callbacks to verify clearing
   the item removes its item-count sort. Shared per-tab item-state ownership and
   broader filter/query interactions remain separately recorded gaps.
4. Use English and Japanese in the compact harness. Browser preview: Truck states
   and disabled native row actions, retained-goods ordering, then Train layout and
   one Japanese view. Use the existing explicit preview fixtures; do not change
   them merely to make a test pass. Time-relative states can advance: reload for
   fresh cases or record actual time. Save one or two images that visibly show the
   relevant columns/rows and inspect those exact files. Record console errors.

Unresolved native images remain placeholders; do not claim asset parity. No live
target tracking, plundering, scheduling, jumping or gameplay action is authorized.

## Verification and delivery

Run the prior check-map-tables.mjs --verify-record and validate-evidence.mjs,
your compact independent checker and evidence validation, npm.cmd run check,
npm.cmd run check:production-build from the UI package directory and git diff
--check. Rebuild only if package checking establishes a concrete need. Preserve
historical reports, including the known stale parent CORRECT-003 Trade assertion.

Allowed writes: this assignment's delivery section and review/evidence under
docs/reviews/<actual-date>-LWB317-REVIEW-MAP-TRANSPORT-001.md and
evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TRANSPORT-001/.
Leave master acceptance to the lead. Return REVIEW_COMPLETE with ACCEPT or
CHANGES_REQUIRED for this exact two-table source/local scope. For defects give
reproduction, source/current locators and expected/actual values. State UI gaps
separately from native/pixel limits. Stop after this one review.

Stay on research/offline-controller; review the diff, stage only owned files,
commit/push and verify direct remote SHA. Preserve unchanged/unstaged:
src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js, .scratch-lwb317/ and parent
CORRECT-003 screenshots. Close only your preview tab/server. No other panels,
backend changes, Last War launch/control, auth bypass, fallback or subagents.
No fixed elapsed-time stop; complete the scope or report a concrete blocker.
