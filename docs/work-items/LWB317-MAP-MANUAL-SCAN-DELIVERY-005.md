# LWB317-MAP-MANUAL-SCAN-DELIVERY-005

2026-10-11. **ASSIGNED** by the lead after owner-directed transition to Map.
Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: `codex/map-manual-scan-delivery-005`; one existing checkout.
Accepted main base: `49c30e45be5418e8f224ced090e1cf71949745ca`.
Resume current descendant; do not reset or import the research branch wholesale.

## Deliverable

A releasable working Manual City/Resource scanning feature through the normal
Windows Map interface, on top of accepted single-game Home and stored Map browsing.
Faithful functional replacement: retain familiar UI, purposes and known results;
document supported own-design acquisition/retry/timing where original internals
are unavailable. Exact protected scanner equivalence is not required or claimed.
Real acquisition, truthful progress/results, persistence and cancellation are.
This is the first Map completion delivery, not acceptance of the entire Map tab.

Read AGENTS.md, docs/OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md,
docs/OWNER_DIRECTION_SINGLE_GAME_HOME_2026-10-11.md,
docs/HOME_004_RELEASE_RECEIPT_2026-10-11.md, docs/MAP_DATA_LEAD_ACCEPTANCE.md,
docs/MAP_005_CURRENT_STATUS.md and docs/LOOP_WORKER_PROTOCOL.md.
This work item supersedes the old Home-only/no-new-Map assignment limits for its
explicit scope. External worker is solo; no subagents/GPT Work/Codex delegation.

## A — current production inventory and meaningful baseline

Trace packaged MapDataPage/App/native dispatch -> Map317CommandService -> current
provider/engine -> staged/published SQLite rows. List the remaining concrete
City/Resource defects or missing witnesses in one compact queue. Reuse stored
Map query/export and historical capture/cancellation work instead of starting over.
Identify current admitted game content/build; earlier v22/v24 or server2212 are
historical, not assumptions. Read-only compatibility and reference hash gates
precede live activity. Older inspectors/tools absent on main can be read using
`git show origin/research/offline-controller:<path>`; adapt only the needed narrow
checks, without copying bulk evidence or stale original-only acceptance policy.

## B — finish actual scan/control behavior

Use current production implementations. Verify City and Resource selection,
Normal/Fast admission and concurrency, empty selection's documented normalization,
Start/duplicate-start fencing, progress/errors, scan ownership, completion and
finished publication. Fix reproducible defects; do not invent success or silently
use old published data as a fresh capture. Preserve other kinds' existing data.

Verify actual engine/sink/native Stop and SQLite with controlled held responses:
Stop before completion, after positive staging, late provider return, publication
race, profile/view change, backend loss and host exit. A cancelled run must not
publish its new rows or alter previous successful publication. Stop must retire
its exact run, not a successor or another profile. Keep accepted Home cancellation,
repair, adoption and shared-package gates intact.

These meaningful controlled tests cover rare races; do not demand unsafe live
faults or endlessly attempt a naturally unobservable positive-staging interval.
Distinguish acquisition progress, staged records and published records.

## C — bounded genuine packaged Map witness

Within the previously authorised Home/current-server Manual City/Resource pilot,
use one isolated game/profile/database and exact process/session/build/backups.
Use actual Home Launch -> authenticated Connected, then real packaged Map controls
for separate current-server City and Resource completed scans. Record actual fresh
run IDs, server/world, selected types, progress, completion and newly published
rows. Counts are observations, not fixed goals of 7,000/8,008. Cross-check actual
`map_summary`, `map_search`, `map_data_options` and reopened SQLite identities/values.
A helper-only capture or preloaded archive is not this UI/native witness.

Perform one separate bounded active scan Stop through the Map control. Verify
terminal cancellation/no new publication and preserved previous data. A natural
positive-staged live Stop is useful if observable, not a reason to repeat indefinitely;
controlled production-boundary staging/race proof remains required and labelled.

Verify search/name/level filters, sorting, pages and tab return on these fresh rows;
local City player marks if that existing control is available; City Excel export
through native Save As and independent workbook reopen against the fresh query.
Resource export is not an original supported control; do not invent one. Close and
reopen the host, reconnect only as needed for the current supported browse-server
context, and show persisted rows without another scan. Exercise Clear only on the
task-owned database after preserving the relevant result receipt; never owner data.

Capture concise EN/light and JA/dark evidence, actual narrow viewport and useful
error/busy/Stop feedback. Keep real player datasets and raw captures out of Git
and releases; commit only compact sanitized receipts and reproduction instructions.
End each genuine attempt with exact Home Close, restoration hashes, no owned game/
launcher/host processes, no pending journal and no orphan scan/pipe subscriptions.

## D — release candidate and independent handoff

Self-review the actual diff, preserve negative attempts, run applicable current
checks, fix valid failures inside the loop and build one final package after code
settles. Required baseline commands:
- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
- dotnet run --project tests/LWBridge.Desktop.Checks -c Release
- dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release
- dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
- canonical production UI/package identity checks and git diff --check
Add distinguishing actual Map engine/handler tests to the current maintained suite.
Historical script filenames or obsolete extraction shapes are not mandatory;
use actual current production boundaries without weakening assertions.

Verify extracted ZIP source/EXE/UI identities and meaningful packaged Map controls.
Run affected Home regression; no need to repeat its entire accepted live campaign
unless a changed boundary requires it. Open one PR to main, attach the source-
identified Windows ZIP, accurate scope/limits and compact review. Commit coherent
milestones and push this branch normally; verify the direct remote SHA.

Return READY_FOR_LEAD_REVIEW when this scan delivery passes. If an actual external
capability/input prevents an operation, finish independent ready items and return
PARTIAL with precise failure, attempts and continuation. Ordinary failed tests are
not a reason to stop. Worker does not merge/publish; the lead reviews, merges,
publishes and retires the branch after acceptance. Whole Map stays PARTIAL.

## Preserved behavior / exclusions

Home single-game release and previous City/Resource browsing/export stay working.
Do not redesign Map, remove expected controls, import old clone-only assumptions,
claim exact encrypted traversal was recovered, disable ownership checks or add a
fake/legacy fallback. No dual-game work, Auto Scan execution, cross-server jump,
coordinate/march navigation experiment, alliance share, plunder, Treasure claim,
spending, combat, updater, protected services or account bypass in this delivery.
Other scan kinds are affected regressions only; never enable action workers/jobs
as a side effect of starting the test host. Inspect existing persisted jobs and
keep the isolated task root free of actionable jobs/recurring Auto configuration.

Existing Home + current-server City/Resource live permission applies; explicit
pause/stop and actual approval denials prevail. Use available Windows-MCP and
Remote Desktop Commander as complementary tools; record actual limitations and
continue headless ready work where a UI step cannot run. Do not alter owner network,
installation/account settings or unrelated processes. No installation restoration
while any game using it survives. There is no fixed deadline or success percentage.

## Current continuation

ASSIGNED; no new Map implementation/live experiment performed by the lead.
Next: read current main-derived source and make the compact City/Resource queue,
then proceed through A–D without returning after each routine failure.
