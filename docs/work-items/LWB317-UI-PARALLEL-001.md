# LWB317-UI-PARALLEL-001 — parallel Map correction and Trade closeout

Project-lead assignment: 2026-10-02. State: COMPLETE for assigned source/local scope.
Owner requested lead takeover while the worker rests. PM-022 integrates all three
lanes, accepts independently reviewed Map row corrections/basic tables and Trade
status/inactive-fixture corrections after source/browser/integrated checks. No
active implementation remains in this assignment. Historical execution rules
below describe the completed campaign, not an instruction to restart it.
Delivery: docs/reviews/2026-10-02-LWB317-PM-022-parallel-takeover.md.
Owner requests faster UI progress and authorizes the worker to use two subagents.
This replaces the previous single active Truck/Train review assignment. It does
not reopen the broad interrupted CORRECT-003 campaign or any native campaign.

## Fresh-chat context and authority

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`. Start from current HEAD without reset.
Product/evidence baseline before this assignment: `266ce2a` (resolve full SHA).
Read `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`,
`docs/PROJECT_LEAD.md`, `docs/lwbridge-ui.md`, and this assignment first.
The current assigned work is UI reproduction only, regardless of historical
function campaigns recorded in the master documents.

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.
Verify original identities; every recovered fact needs a source and UTF-8 byte
locator or equivalent exact locator. Original 0.3.1 findings are historical.

The lead independently reran the transport review checker and evidence validator
at 266ce2a: CHANGES_REQUIRED, 14 mismatches grouped into two defects. Preserve its
historical results. Accepted Home, weekly settings, Trade selection/history/
cross-server switch, and three Map task/Treasure table units must stay stable.
Full post-auth pixels and live gameplay are not established by source/local QA.

## Parallel execution and ownership

You are the coordinator/main worker. Spawn exactly two subagents, give each the
complete context, constraints and acceptance criteria for its lane, and forbid
nested delegation. Do not delegate merely to have both repeat your own work.
This assignment explicitly permits subagents despite prohibitions in earlier
individual assignments, only for the lanes below. The retired Chat On Steroids
policy imposes no setup, model or review requirements.

- Main worker owns Map production edits, Map preview fixtures, integration,
  shared documentation, browser coordination, Git and package/build commands.
- Subagent A owns Trade status/loading/error review and any narrowly necessary
  correction within `TradeStationCard` in `src/LWBridge.UI-0.3.17/src/Pages.jsx`
  and Trade-only cases in `previewAutomationFixtures.js`.
- Subagent B is read-only on product code. It owns an independent
  City/Resource/Monster table review and its isolated evidence/report files.
- Each lane writes only its own evidence directory and review report. Children
  must not edit master documents, commit, push, run builds, modify generated
  packages, or change files assigned to another lane. Ask the coordinator to
  integrate a cross-file change instead.
- Before concurrent edits, pin the pre-correction Map baseline and capture the
  Map files needed by B into its own evidence working area, recording hashes.
  B reviews this immutable baseline; rerun its assertions on final integrated
  code to detect regressions. Do not compare a moving shared checkout silently.
- Parallelize source analysis and focused checks. Serialize shared browser use,
  dependency installation, builds, final checks, staging and commits. Only close
  this assignment's own preview tabs/listeners. Preserve owner/other AI activity.

## Main lane — LWB317-UI-MAP-ROWS-001

Read `docs/reviews/2026-10-02-LWB317-REVIEW-MAP-TRANSPORT-001.md` and the earlier
LEAD-TABLES review/evidence. Correct both independently confirmed defects:

1. Truck and Train Live Target cells: original `de`, Map asset byte 15490, shows
   `-` without `marchUuid`, otherwise Follow/Following with the recovered
   tracking/disabled rules. Current cells incorrectly show coordinates. Recover
   the complete relevant presentation inputs, use existing frontend state where
   available, and expose missing branches through disclosed browser-only QA
   fixtures. Preserve the native-unavailable action fence; do not add a tracking
   provider, fake native success, or make every new button permanently disabled
   merely to avoid recovering its display predicates.
2. Truck selection labels: original `me`, byte 17072, uses
   `String(ownerName || allianceName || uuid)` and raw `serverId`. Remove invented
   hyphen substitutions while preserving original eligibility and localization.

Production ownership: `MapDataPage.jsx`, its focused presentation helpers and
Map-only preview fixtures. Do not edit App lifecycle or native bridge contracts.
If B finds additional City/Resource/Monster normal-table defects, independently
verify and correct them in this lane. Bound these corrections to columns, cell
rendering, row labels/selection, and existing frontend presentation state.
Broader filters, Scheduled Plunder, Treasure refreshing producer, new native
actions and missing game assets remain separate named backlog items.

Acceptance: original/current executable comparison of actual production paths;
missing/present march UUID, Follow/Following, relevant disabled states, empty/
missing names, zero/missing server, English/Japanese, and preservation of other
table kinds. Real browser QA must visibly exercise the new branches with native
operations fenced unavailable. Treat synthetic values as QA only.

## Subagent A — LWB317-REVIEW-TRADE-STATUS-001

Read `docs/work-items/LWB317-UI-CORRECT-003E.md`, its delivered review/evidence,
and accepted Trade 003B/R1, 003C, 003D findings. Independently compare recovered
`AutomationPanel-BJ0gIqFh.js` Trade renderer/effects with the actual current code.
Original asset SHA-256:
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.

Review only detected/attempted/succeeded counters, last-result translation/
defaults, goods loading/empty/fetch-error text, retained goods/purchase history,
and Goods/Purchased tab preservation. Keep config-save errors, goods-fetch errors
and purchase-result state distinct. Confirm source conditions rather than deriving
expected outputs from the clone. Do not rely only on the author's checker.

Independently reproduce any defect and implement a focused correction in your
owned files. Preserve immediate/concurrent saving, Retry/Discard, currency/goods
composition, purchase history grouping and cross-server toggle behavior. No
other Automation cards, AFK, purchase execution or native fetch/status providers.
After modifying code, report correction as AWAITING_REVIEW; an unchanged review
may recommend ACCEPT. Neither constitutes project-lead acceptance.

Acceptance: independent distinguishing original/current render/callback checks,
absent/zero/populated status, source-supported result states, loading/error with
retained data, tab switching, English/Japanese and the existing focused Trade
regressions. Coordinator schedules real browser QA for loading, supplied error
text and retained Purchased data. Save at most two useful inspected images.

## Subagent B — LWB317-REVIEW-MAP-BASIC-001

Review City, Resource and Monster normal-table presentation only. Use exact
`MapDataPanel-B4GXEND2.js`, its column factory `nt` at byte 9594, relevant renderer
helpers, index/locale dependencies and the pinned production baseline.
Map asset SHA-256:
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

Independently execute relevant original and actual production expressions. Check
column order/width/sort metadata, names/levels, missing and unknown values,
quality/power/count formatting, times and boundaries, row identity/selection,
coordinate/mark cell presentation and disabled predicates where applicable.
Use small distinguishing cases in English/Japanese, not a huge repetitive matrix.
Schedule browser checks for all three kinds with coordinator; record visible
labels, representative populated/empty rows and fenced native controls.

Do not modify product code. For each defect provide exact source locator,
current file/line, minimal input, original/current output and correction scope.
Report source/local recommendation separately from original pixel/native limits.
Do not broaden into scan, query/filter interactions, scheduled plunder, gameplay
or unrelated table kinds. Coordinator integrates verified in-scope defects.

## Evidence, integration and delivery

Use each lane ID for `evidence/lwbridge-0.3.17/ui/<ID>/` and dated
`docs/reviews/YYYY-MM-DD-<ID>.md`. Record original/current hashes, source locators,
actual-code checks, browser actions/results, inspected images/hashes and limits.
Preserve historical failed reports; create new correction evidence rather than
rewriting old results to pass. Historical current-source hashes may legitimately
drift: run old assertions on current code separately, record the reason, and
do not disable meaningful checks or change expected behavior to hide failures.

Coordinator must independently assess every child finding and inspect every
child diff. Then run integrated Map/Trade focused regressions, relevant accepted
Home regressions, `npm.cmd run check`, `npm.cmd run build`,
`npm.cmd run check:production-build` from `src/LWBridge.UI-0.3.17`, all new evidence
validators and `git diff --check`. Shared checks/builds run once on final code;
repeat only after further changes/failures. No need to rerun every historical
campaign or restart finished investigations.

Update current UI master, relevant matrix/ledger rows, project-lead assignment
delivery and handoff conservatively. Do not declare complete UI/pixel parity or
accept your own corrections. Make coherent explicitly staged milestone commits,
push to `origin/research/offline-controller`, and verify remote SHA. Main worker
alone performs Git mutations. Continue all assigned lanes to acceptance criteria
or a concrete blocker; checkpoint partial lanes without losing others' progress.
No fixed duration or elapsed-time stop rule.

Preserve unchanged AND unstaged:

- `src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`
- `.scratch-lwb317/`
- `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`

Record these existing WIP hashes before edits and verify them at delivery. Never
reset, clean, force-push or stage unrelated work. Exact reference CSS/assets,
login/account/licensing UI, legacy host retirement and original-service access
controls remain outside this task. No Last War launch/control or live functions.
Keep fixtures browser-preview-only and absent from native/native-unavailable
fallback paths; they must never simulate successful native actions.

Return one concise combined delivery: per-lane status, defects actually fixed,
review conclusions, files/evidence, checks, commits/remote SHA, preserved WIP,
remaining gaps and precise continuation point. Coordinator corrections remain
AWAITING_REVIEW for the project lead. Do not start another campaign afterward.
