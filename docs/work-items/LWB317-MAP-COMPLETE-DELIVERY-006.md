# MAP-006 — finish the Map feature catalogue

2026-10-11. ASSIGNED by the lead at the owner's request to tidy the queue and
assign all remaining Map work together. Repository:
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `codex/map-complete-delivery-006`; existing checkout only.
Starting product checkpoint: `023e34d0fae328bfeef5be36858b773a22a8b520`.

MAP-005 / PR #7 remains a separate READY_FOR_LEAD_REVIEW delivery. Its branch,
source, package and receipts are preserved. It is not accepted by this assignment.
MAP-006 starts above it so the work can continue while the lead reviews #7.
Do not modify #7, merge main or create further branches/worktrees. If #7 receives
lead corrections, integrate those corrections normally before final acceptance.

Read AGENTS.md, docs/LOOP_WORKER_PROTOCOL.md,
docs/OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md,
docs/MAP_006_CURRENT_STATUS.md, docs/MAP_005_CURRENT_STATUS.md,
docs/MAP_005_DELIVERY_RECEIPT_2026-10-11.md, docs/MAP_DATA_LEAD_ACCEPTANCE.md,
docs/HOME_004_SINGLE_GAME_LEAD_ACCEPTANCE_2026-10-11.md and this entire work item.
Older research can be read with git show; do not import the research tree wholesale.
External worker works solo; no subagents, other AI chats, GPT Work or Codex delegation.

## Goal and completion meaning

Complete every Map feature exposed by the reference/current canonical interface:
eight manual scan kinds, their browsing and local controls, Auto Scan, navigation,
Treasure status/claim workflow, sharing, and Scheduled Plunder. Preserve released
single-game Home. No dual-game campaign or unrelated page work.

Target is the owner's faithful functional replacement policy. Use known original
purpose/workflow/defaults and verified current-client capabilities. Tested own-design
controllers are allowed where original details are unavailable: document their user
effect and why they meet the feature purpose. Exact encrypted internals are not a
universal prerequisite. Conversely, a send, queue admission, UI toast, synthetic row
or disabled provider is not proof of a successful game result.

Do not promise whole Map completion until every included row meets its real result
gate. If an actual unavailable client capability or required permission prevents a
row, keep that row incomplete and finish all independent ready rows. Do not hide it,
drop the feature, loosen identity checks or label provider-unavailable as working.

## Execution authority

Latest owner clarification, 2026-10-11: server switching is allowed; worker
must not execute live plunder, claims or sharing because the owner will do those.
Read docs/OWNER_DIRECTION_MAP_SERVER_SWITCHING_2026-10-11.md. This supersedes
the older same-server-only/server-switching permission limit below. Include
multi-server scan/Auto target-return checks within the existing technical gates;
do not initiate marches or other account actions. Historical scope quote below
is retained for provenance, not current server-switching authority.

Implement and inspect all Map paths; static, headless and isolated controlled tests
cover the entire catalogue. The owner has requested completion of all Map work.
Existing bounded Home/current-server scan permission applies. This assignment also
covers passive acquisition of the remaining types and a bounded same-server Auto
scan with task-only configuration, stopped before leaving the test session.

Owner's explicit answer, 2026-10-11:
"Finish all code; live-test scans and same-server Auto only. Ask before claims,
plunder, sharing or jumps."

Live claims, plunder, alliance sharing and jumps (including coordinate, march-follow
and cross-server navigation) are not authorised in this assignment. Finish their
code and controlled tests, but ask the owner before executing them live and do not
count them live-verified. No such permission is needed for already-authorised scans
and same-server Auto within the identity/isolation/restoration gates. A pending
action permission is not a reason to stop other ready work. A later affirmative
relay adds only the named bounded actions. Paid spending, purchases, unrelated combat, updater actions,
protected-original services and authentication/entitlement bypass remain excluded.
Check any claim/march/job for resource consumption before live execution; stop if
the approved action would incur unapproved costs or a different game action.

Use an isolated root/profile/database, current build admission, exact process and
session identity, backups and restoration. Never restore installation scripts while
a surviving game still uses them. Explicit owner pause/stop and actual approval
denials prevail; do not bypass. Verify available Windows-MCP and Remote Desktop
Commander capabilities when needed. Do not change owner networking/account settings
to create faults. Never arm existing owner jobs or recurring Auto as a test side effect.

## A — make one finite catalogue, then proceed

Trace actual MapDataPage/App controls and every Map/native command through current
service, provider, Lua/client boundary, engine, scheduler and storage. Build one
compact table with: visible operation, purpose, actual entry point, existing defect
or missing proof, result gate, required live authority and current evidence.
Inventory every control and command, including conditionally rendered ones. Do not
declare the list complete from this document alone. Include assets/names, filters,
sorts, pages, marks, clear, navigation history, progress/error/retry and accessibility.

Mark City/Resource MAP-005 as submitted baseline, not unfinished work to repeat.
Resolve its documented unknown Resource label when recoverable current metadata
exists; do not invent names. Verify the actual current game content/build rather
than assuming historical v22/v24, server 2212 or fixed player-row counts.

Maintain only this work item, MAP_006_CURRENT_STATUS.md and a compact differences
ledger/feature matrix as current pointers. Keep immutable prior failures/receipts
unchanged. Do not produce another huge evidence archive for every checkpoint.

## B — complete all manual acquisition and stored-data paths

Also read docs/MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md. Distinguish recovered
original host contracts from own-design acquisition. Assess whether type-specific
lists/direct region requests can preserve coverage with less view movement and
lower acquisition cost; benchmark supported alternatives before claiming best
efficiency. Preserve the working baseline and do not trade completeness for speed.

Assess and finish Monster, Truck, Train/Railway, Secret Task/Dispatch, Ghost Ops
and Treasure, preserving City/Resource. Verify actual source populations, schemas,
identities, server scope, timestamps/units, quality/status/name resolution and assets.
Use direct current-client producers for transient/train data when grid acquisition
is not their source. No fabricated fixture population or fallback to another kind.

Prove each kind's fresh acquisition -> staging -> completion -> publication ->
native summary/options/search -> actual UI -> persistence/reopen. Test mixed and
single-type publication, retained other-kind rows, Normal/Fast, duplicate Start,
empty selection normalization, failures, late replies, cancellation/publication
races, server/profile/session change and host exit through production boundaries.
Normal filtering of empty data is a valid empty result but not proof of a working
positive extractor. Seek a bounded naturally available positive witness; explain
the actual population dependency if none is available instead of endlessly scanning.

Keep browsing filters/sorts/page/tab state source-compatible where practical;
remove stale-response races and wrong-profile results. Verify all currently supported
exports; do not invent Resource export. Clear/marks touch only task data. Real player
rows, raw game scripts and private captures stay in ignored local artifacts.

## C — finish Auto Scan through the actual scheduler

Verify enabled/configuration persistence, server lists and ordering, deduplication,
last-type protection, hydration/concurrent edits, interval/speed/type/return settings,
Run Now, countdowns, cancellation, overlapping manual/Auto admission, reconnect,
restart and profile ownership. Read actual existing source before selecting disable
semantics: distinguish stopping the next cycle from cancelling an active scan.

Use injected clocks/providers for long intervals and adversarial races. Run a genuine
bounded same-server cycle through packaged controls, showing its actual fresh run,
completion, result publication, and that disabling prevents another cycle. Verify
multi-server target/return behavior under controlled production providers, then live
only if the specific cross-server permission is present. No unattended recurring
tasks may remain enabled at cleanup or continue after host/session teardown.

## D — row navigation, inspection and sharing

Finish coordinate jump, march follow, server jump and history, player marks, asset
and name loading, Treasure state refresh/batching and all row-specific controls.
Verify actual producer identity, profile/server scope, current-state changes,
unavailable/offline feedback and loading/error lifecycle. Navigation means the real
client reached the requested view, not merely successful command dispatch.
Sharing means an authoritative acknowledged share; timeout/rejection is not success.
Live account-affecting steps wait for their specific authority, not for another full
implementation campaign. Do not use a share to spam chats or test broadcasts in bulk.

## E — Treasure and Scheduled Plunder end to end

Current explicit gaps include GetTreasureClaimStatusAsync, ClaimTreasuresAsync and
PrepareGhostPlunderTasksAsync. Trace existing original recovery and actual admitted
client producers/handlers first. Replace incomplete providers only with supported
implementations; use documented own-design ownership/orchestration if appropriate.

Treasure: identity/status ownership, eligible state, single/boxes/season modes,
lucky priority, limits, batch counters, missing/refreshing states, partial failures,
cooldown/reset, profile switch/reconnect, cancellation and authoritative reward or
claimed-state completion. A sent request or newly created march is not reward proof.

Scheduled Plunder: Dispatch, Ghost and Truck creation, validation, delay/ordering,
protection/expiry, server-day/attempt limits, countdown/status, scheduling/restart,
Cancel/Clear/Retry/Plunder Again, history, deduplication and exact terminal correlation.
Ghost must not use Dispatch transport. Managed future IDs can be reused on reconnect;
scope correlation to connection/session and durable job identity, reject ambiguous
or absent identities. Prevent stale acknowledgements retiring successor jobs.
Verify with actual production boundary tests, including failure/timeout/late result,
disconnect, restart, cancellation and profile/session changes. If live permission
is granted, perform the smallest eligible bounded action and verify the actual
terminal result. Do not make automatic repeated attempts that consume resources.

## F — integrated package and delivery

Self-review the complete diff; fix regressions and retry meaningful failed cases.
Run maintained native/frontend checks and add distinguishing actual-boundary checks
for changed capabilities. Existing baseline commands:

- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
- dotnet run --project tests/LWBridge.Desktop.Checks -c Release
- dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release
- dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
- canonical production UI/package checks and git diff --check

Verify actual packaged controls and native provider wiring. Capture concise settled
EN/light and JA/dark/narrow coverage for changed surfaces and important states;
label controlled/live separately. Reuse unchanged accepted evidence with identity
links; rerun affected Home regressions rather than reopening whole Home.

End live work with exact Stop/Close, saved-state reconciliation, restored original
hashes, no task game/launcher/host, no journal, subscriptions, active Auto or jobs.
Preserve failed attempts and compact cleanup receipts. One final source-identified
Windows ZIP after code settles; extract/recheck EXE/UI/source identity, absence of
private files and actual startup/controls. Never ship task credentials, data or jobs.

Open one MAP-006 PR with an accurate table of working, controlled-only, live-pending
and incomplete features. Initially note its dependency on MAP-005 PR #7. The lead
merges #7 first if accepted, then reviews #006's incremental changes; worker never
merges/publishes. Commit coherent milestones, push normally and verify remote SHA.

## Loop and return boundary

Diagnose -> supported fix -> distinguishing test -> affected regression -> self-review
-> checkpoint -> next ready item. Do not return merely because a checkpoint finishes,
a test fails or a fixed time passes. Continue through A-F without repeated relays.
After compaction/interruption, inspect the saved queue/HEAD and resume rather than
redoing finished work. No random changes or assertions weakened to manufacture green.

Return READY_FOR_LEAD_FUNCTIONAL_REVIEW only if every included operation passes its
truthful gate. Otherwise finish all ready work and return PARTIAL with exact remaining
operations, missing capability/permission/input, attempted supported alternatives,
proof, cleanup and continuation. Do not claim whole Map is done from checks alone.
Only the lead accepts, merges, publishes and retires merged branches/worktrees.

## Current continuation

ASSIGNED. Lead has inspected the current queue/provider gaps; no MAP-006 product
implementation or live experiment performed. Start with A, then continue B-F.
Owner live scope now includes server switching and multi-server passive scans/
Auto. Worker must not execute live claims/plunder/sharing; owner will do those.
Complete all code/controlled gates and allowed live work when resuming after the
discussion; record owner-only witnesses as pending, not passed.
