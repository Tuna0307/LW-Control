Lead checkpoint, 2026-10-08: **LIVE-PILOT-001 PARTIAL / BACKGROUND ONLY**.
Attempt-5 historical readiness/direct-reader evidence is preserved at product
checkpoint b1f75a25. No fresh Resource result or complete pilot sequence is accepted.
Lead restored the exact original game script triplet, cleared pending recovery and
verified no LastWar/LWBridge processes; 5 ownership + 6 Lua lease tests and current
v22 structural admission pass. Shared-desktop screenshots/input/focus are now
restricted by the owner; background Last War remains permitted without interruption.
Next solo medium task: LWB317-FUNCTION-HOME-MAP-BACKGROUND-CONTINUATION-001.
See docs/reviews/2026-10-08-LWB317-LIVE-PILOT-CHECKPOINT-LEAD.md. Earlier continuation
paragraphs below are historical where inconsistent. Home/Map remains PARTIAL.

# LWB317-RE-MAP-001 — Map Data end-to-end recovery goal

**State:** AWAITING_REVIEW
**Phase:** 2 — function recovery
**Project-lead authorization:** 2026-09-30

This is a deliberately large Goal-mode assignment. It is intended to require
multiple continuations and substantial independent reasoning.

## Goal

Recover the LWBridge 0.3.17 **Map Data function family** from exact reference
evidence deeply enough that the project has:

1. an evidence-backed frontend/host contract;
2. an evidence-backed persistence/query/export contract;
3. an evidence-backed scan lifecycle/state machine;
4. a clearly separated current-client compatibility map;
5. a tested local implementation of the recovered non-game control/data plane;
6. a precise list of remaining game/provider/native unknowns.

Do not stop after finding command names. Trace the family end-to-end as far as
the exact 0.3.17 artifact permits.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Expected SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Read first

1. `AGENTS.md`
2. `task.md`
3. `docs/README.md`
4. `docs/PROJECT_LEAD.md`
5. `docs/AI_WORK_PROTOCOL.md`
6. `docs/strict-parity-recovery.md`
7. `docs/lwbridge-feature-ledger.md`
8. `docs/lwbridge-parity-matrix.md`
9. `docs/reviews/2026-09-29-LWB317-UI-002C-map-data-inventory.md`
10. `docs/LEGACY_0.3.1_INDEX.md`
11. this file

Inspect `git status --short` and current history before acting.

## Historical 0.3.1 policy

The repository contains extensive old Map research and tooling. Use it
aggressively as a hypothesis/search accelerator, but never promote a 0.3.1
contract to 0.3.17 fact without revalidation against 0.3.17.

For every reused historical claim, classify it as:

- unchanged in 0.3.17;
- changed in 0.3.17;
- absent in 0.3.17;
- still unknown.

## Scope

Recover the complete in-scope Map Data family visible or referenced by 0.3.17,
including where evidence exists:

- Manual Scan;
- Auto Scan / scheduling;
- scan start;
- scan resume/restart behavior;
- scan status/progress;
- scan stop/cancel;
- scan clear/reset;
- scan summary;
- scan options/content selection;
- Player City acquisition/data;
- Resource Point acquisition/data;
- Monster acquisition/data;
- Truck acquisition/data;
- Train acquisition/data;
- Secret Task acquisition/data;
- Ghost Ops acquisition/data;
- Treasure acquisition/data;
- saved-data tabs and counts;
- City search/filter/sort/pagination;
- Resource search/filter/sort/pagination;
- Monster search/filter/sort/pagination;
- Truck/Train/Secret Task/Ghost Ops/Treasure query behavior;
- Scheduled Plunder data surface where present;
- coordinate/map jump behavior;
- cross-server/server jump behavior and history where present;
- city export / Excel behavior;
- player marks/notes where present;
- Map-related persistent database/storage ownership;
- Map-related Tauri commands/events;
- frontend request/response schemas;
- error/cancel/terminal-state behavior;
- concurrency/ownership semantics visible in the 0.3.17 host.

Treat Map Data as one coherent product subsystem.

## Required trace depth

For each recovered function, trace as far as evidence permits:

`UI trigger -> frontend function -> Tauri invoke/event -> host handler -> state/storage -> provider/native boundary -> visible result`

Do not stop at a JavaScript command string if the native host can be recovered
further.

Where the trace reaches game-specific material that cannot yet be established,
stop at the exact boundary and record it.

## Static/native recovery requirements

Use appropriate static techniques against the exact 0.3.17 binary and recovered
frontend package, including as useful:

- JavaScript/component/locale analysis;
- Rust/Tauri command-string cross references;
- PE section/string/constant analysis;
- targeted disassembly/decompilation;
- database/schema/query string recovery;
- serializer/field-name recovery;
- event-name recovery;
- state-machine reconstruction;
- comparison against old 0.3.1 findings only after 0.3.17 evidence is located.

Create version-specific helper tools under:

`tools/lwbridge317/`

Do not edit old 0.3.1 evidence to make it appear current.

## Implementation requirement

Do not merely write research notes.

Create a **new 0.3.17 Map control/data-plane implementation** separate from the
legacy 0.3.1 reconstruction. The worker may extend
`src/LWBridge.UI-0.3.17` with a local service/adapter layer or create a new
clearly named 0.3.17 companion project if that gives cleaner boundaries.

The implementation must:

- model recovered Map command/request/result schemas;
- model recovered scan lifecycle states;
- model recovered persisted/queryable Map records;
- implement evidence-backed local query/filter/sort/pagination semantics;
- implement evidence-backed export formatting where recoverable;
- implement evidence-backed marks/history/options storage where recoverable;
- expose a clean boundary for the still-unimplemented game/provider adapter;
- never fabricate live game results;
- label unavailable provider-backed actions as unavailable rather than
  pretending success.

The existing Phase 1 Map UI may be wired to this local recovered control/data
plane where doing so is evidence-backed.

## Tests

Build a substantial deterministic test suite from exact recovered contracts.

At minimum cover:

- command argument validation;
- scan-state transitions;
- cancel/stop terminal behavior;
- resume/restart semantics where recovered;
- count/summary behavior;
- every recovered query filter family;
- sorting direction/defaults;
- pagination;
- unknown/null data fidelity;
- export row/filename behavior where recovered;
- marks/history/options persistence where recovered;
- malformed/corrupt persisted data behavior;
- ownership/concurrency edge cases where recoverable.

Do not claim live proof from deterministic/local tests.

## Current-client compatibility and live validation

After the 0.3.17 contract is stable, investigate the currently installed Last
War artifacts statically/headlessly first, then proceed to **bounded live Map
validation against the current Last War client**.

Live Last War interaction is explicitly authorized for this Goal because Map
scan correctness cannot be established from static analysis alone.

Before live interaction:

- inspect existing Last War/LWBridge processes;
- treat unexplained existing game sessions as owner activity;
- do not close, steal, repurpose or mutate an owner session;
- prefer a worker/assistant-owned game session for disruptive tests;
- record server/world/player context needed to interpret results;
- fail closed if session ownership is ambiguous.

The worker may launch/control an assistant-owned Last War session and perform
Map-only actions needed to validate the recovered subsystem, including:

- current map/world-state observation;
- coordinate navigation/jump;
- server jump when required by the recovered Map contract;
- manual scan start/status/progress/stop/clear/resume;
- auto-scan scheduling/status where safely testable;
- acquisition of city/resource/monster/truck/train/secret-task/ghost-ops/
  treasure data where available;
- query/filter/sort/pagination checks against acquired records;
- export/marks/history/options validation when they do not cause unrelated
  gameplay effects.

Do not send unrelated gameplay actions, marches, purchases, attacks, resource
spending, alliance actions or automation outside the Map subsystem.

Create a compatibility matrix covering:

- exact 0.3.17 expectation;
- old 0.3.1 historical implementation if relevant;
- current-client candidate source;
- unchanged/changed/missing status;
- confidence;
- what still requires live proof;
- what was actually live-proven in the current client.

### Current v22 bounded live checkpoint — 2026-09-30

`LWB317-LIVE-MAP-V22-001` has fresh assistant-owned current-v22 evidence for
runtime/Map readiness, world metadata, coordinate navigation, scan start/status,
one complete fast Resource acquisition (`2500/2500`, zero failed/unread, 678
rows), query/filter/sort/pagination, clear/reset and orphan-free cleanup. See
`docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-001.md` and
`evidence/lwbridge-0.3.17/map/LWB317-LIVE-MAP-V22-001/`.

The optional controlled stop/cancel attempt did not establish a new reading run
because the owned game connection was unavailable after the required completed
scan/query/clear proof. Stop/cancel therefore remains
`IMPLEMENTED_NOT_VALIDATED`, and the cause of that post-clear connection loss is
`UNKNOWN`. Restart/resume and server jump were explicitly excluded from this
bounded task. The Goal remains `IN_PROGRESS` pending project-lead review and any
separately authorized remaining Map live work.

`LWB317-LIVE-MAP-V22-002` then performed the bounded stop/clear continuity
follow-up in a fresh assistant-owned v22 session. It live-proved a genuinely
active normal Resource run before `map_scan_stop`, preserved the same
profile/instance/PID/server and healthy Map readiness after stop, cleared to the
default zero/idle state without losing readiness, started a second Resource run
in that exact same owned session, stopped and cleared it, and remained Map-ready
through the final checkpoint. See
`docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-002.md` and
`evidence/lwbridge-0.3.17/map/LWB317-LIVE-MAP-V22-002/`.

The first 002 attempt also proved that a concurrent single-slot current-client
`world-state` status probe could time out and be surfaced as
`GAME_CONNECTION_UNAVAILABLE` while the owned game/session heartbeat and world
context were still healthy. The minimal per-source serialization fix is covered
deterministically and the fresh post-fix live run did not reproduce the false
connection loss. This does not retroactively prove the exact historical 001
connection-loss cause, which remains `UNKNOWN`. Server jump and restart/resume
remain explicitly outside this checkpoint.

### Resource completeness checkpoint — 2026-09-30

`LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` re-evaluated the Resource
population rather than treating the earlier 678-row result as a census. Its
instrumented baseline still completed all `2500/2500` logical blocks, but from
1,703 Resource candidates it accepted only 296 and rejected 1,407 as
`outside_selected_aoi`, with only 206 of 10,000 native AOIs populated and bounds
limited to `x=170..998`, `y=0..799`.

Live runtime spot checks then proved the cause: genuine remote `ResPointInfo`
Resources were present after navigating/holding the target AOI, while the
production-equivalent request restored the camera in the same tick before the
correlated remote response was serialized and returned zero matching Resources.
The current-client correction is Resource-scoped: it defers restoration until
the remote response and Resource serialization complete, restores the original
camera/native state, forces a normal home-view update, and waits for that restored
response before the next remote Resource batch.

Two corrected full-world scans completed `2500/2500` with zero failed/unread
blocks and zero Resource-candidate rejections. They produced 8,008 and 8,007
unique Resources respectively, covered the full `(0,0)..(999,999)` bounds, and
reconciled source/search/summary/options counts. The latest 53 duplicate
occurrences are all `repeated_same_resource_identity` with
`sameSemanticIdentity=true`; no dedupe change is supported by the evidence.

This checkpoint classifies the corrected observable Resource route as
`CURRENT_PATH_COMPLETE` / `LIVE_PROVEN`. It does not prove every Resource object
the game could ever know about, so `GAME_UNIVERSE_COMPLETE = UNKNOWN`; exact
equivalence to original LWBridge's protected/private traversal is also `UNKNOWN`.
See
`docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001.md` and
`evidence/lwbridge-0.3.17/map/LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001/`.

### Clean Map UI integration checkpoint — 2026-09-30

`LWB317-MAP-UI-INTEGRATION-001` connected the clean
`src/LWBridge.UI-0.3.17` Map page to the established Desktop WebView2/native Map
contract while preserving the existing bundled `src/LWBridge.Desktop/WebUi` as
the normal default. A proof-only `--ui-root` host path loads the Vite build
through the real Desktop bridge without broadening the normal privileged UI
origin.

That describes the state of the 2026-09-30 integration checkpoint; the
2026-10-01 production frontend checkpoint below supersedes its frontend-default
selection.

The clean page now uses real connection state, manual Start/Stop/Clear,
scan-status events/polling, per-kind summary counts, backend `map_search`
pagination/sorting and recovered Resource/Monster/City filter plumbing. Browser
preview remains explicitly non-native and does not fabricate production data.

The final assistant-owned v22 acceptance selected Resource Point only and
completed a normal `2500/2500` scan on server `2212` with zero failed/unread
blocks. The fresh live snapshot contained 7,960 Resources. The clean UI queried
and rendered real backend rows, page 2 returned 50 rows with zero page-1 key
overlap, Resource-name filter `100281` returned 2,726 rows, and Clear produced a
zero backend query plus zero/empty rendered Resource state. The same owned Map
session/server remained healthy and the clean UI settled at `Connected` after
clear. See `docs/reviews/2026-09-30-LWB317-MAP-UI-INTEGRATION-001.md` and
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-INTEGRATION-001/`.

This establishes `LIVE_PROVEN` for the clean UI's Resource manual flow. Other
Map tab query/count plumbing is `IMPLEMENTED_NOT_VALIDATED` in this task. Auto
Scan/server jump remains `IMPLEMENTED_NOT_VALIDATED`; restart/resume and
cross-process saved-browse recovery remain open; Treasure claim/status and Ghost
preparation remain `BLOCKED`.

### Production frontend checkpoint — 2026-10-01

`LWB317-MAP-UI-PRODUCTIONIZE-001` promotes `src/LWBridge.UI-0.3.17` from the
proof-only integration route to the canonical Desktop frontend. Release builds
now build and verify that project into a known `ProductionUi` package with
source/artifact fingerprints. A normal zero-argument Desktop launch requires
that package and does not silently fall back to legacy assets. The existing
`src/LWBridge.Desktop/WebUi` is preserved as an explicit `--legacy-ui`
recovery/reference route; arbitrary `--ui-root` remains proof-gated.

The bounded normal-launch v22 acceptance loaded the canonical UI in native mode,
smoke-rendered Home, Automation, Map Data, Squads / AFK, City Layout, Hotkeys,
Mini Games and Settings, then completed a Resource-only 2500/2500 scan with zero
failed/unread blocks. The live snapshot contained 7,994 Resource rows; page 2
rendered 50 distinct rows, Resource-name filter `100281` returned 2,740 items,
Clear rendered zero rows/count, and the same game instance remained connected.
Runtime diagnostics reported no console/page/request/HTTP/CSP errors. See
`docs/reviews/2026-10-01-LWB317-MAP-UI-PRODUCTIONIZE-001.md` and
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/`.

The ordered 2026-10-01 Map continuation has since completed server-jump/history,
restart/reopen recovery, remaining-v22 category acquisition, safe action wiring,
and Auto Scan recovery. See the five subsequent Stage A–E reviews and the
final closeout review. The Goal is now `AWAITING_REVIEW`, with the remaining
validation/provider boundaries recorded there rather than erased.

## Live proof requirements

Where the current game/server state permits it, the Goal should not stop at
`IMPLEMENTED_NOT_VALIDATED`.

Attempt to live-prove at least:

1. connection/runtime readiness required by Map;
2. map/world metadata read;
3. coordinate navigation;
4. scan start;
5. progress/status updates;
6. safe stop/cancel;
7. clear/reset;
8. at least one completed scan/acquisition path that produces real records;
9. query/filter/sort/pagination over those real records;
10. restart/resume semantics when safely reproducible;
11. no orphaned worker/game process after cleanup.

If a particular content type has no live examples during the test window,
record that as `BLOCKED_BY_LIVE_STATE` / equivalent narrative rather than
fabricating proof.

## Explicit non-goals

- no login/account/licensing reconstruction;
- no auth/entitlement bypass;
- no credential/token/license/purchase protocol work;
- no unrelated Automation/Squads/City Layout function recovery;
- no broad redesign of the Phase 1 UI;
- no fabricated defaults or runtime data;
- no claim of one-for-one runtime visual parity;
- no promotion of old 0.3.1 evidence without 0.3.17 revalidation.

## Required durable outputs

Create/update:

1. `docs/reviews/2026-09-30-LWB317-RE-MAP-001-frontend-host-contract.md`
2. `docs/reviews/2026-09-30-LWB317-RE-MAP-002-scan-state-machine.md`
3. `docs/reviews/2026-09-30-LWB317-RE-MAP-003-storage-query-export.md`
4. `docs/reviews/2026-09-30-LWB317-RE-MAP-004-legacy-revalidation.md`
5. `docs/reviews/2026-09-30-LWB317-RE-MAP-005-actions.md`
6. `docs/reviews/2026-09-30-LWB317-RE-MAP-006-current-client-compat.md`
7. `evidence/lwbridge-0.3.17/map/` with machine-readable manifests/contracts
8. version-specific Map helper tools under `tools/lwbridge317/`
9. the new 0.3.17 Map control/data-plane implementation and tests
10. conservative updates to `docs/lwbridge-feature-ledger.md`
11. conservative function rows in `docs/lwbridge-parity-matrix.md`
12. `docs/reviews/2026-09-30-LWB317-RE-MAP-GOAL-handoff.md`

## Evidence states

Use:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

Never collapse static proof, local implementation and live proof into one
status.

## Git/checkpoint discipline

This is a long Goal. Use multiple coherent checkpoints.

After each major sub-deliverable:

1. finish durable evidence/review;
2. run applicable tests;
3. run `git diff --check`;
4. review `git status --short`;
5. commit coherently;
6. push `research/offline-controller`;
7. continue.

Do not wait for project-lead review between the sub-deliverables in this Goal.
Do not force-push.

## Goal completion criteria

This Goal is complete only when all of the following are true:

- the Map frontend/Tauri command surface is inventoried from 0.3.17;
- the Map scan lifecycle/state machine is recovered as far as the binary
  permits;
- Map persistence/query/export/marks/history/options contracts are recovered as
  far as evidence permits;
- old 0.3.1 Map findings have been explicitly revalidated or rejected where
  used;
- a local 0.3.17 Map control/data-plane implementation exists;
- deterministic tests pass;
- the Phase 1 Map UI is connected only to evidence-backed local behavior where
  implemented;
- the current-client compatibility matrix exists;
- every unrecovered provider/native boundary is listed precisely;
- bounded current-client Map live validation has been attempted;
- each claimed live Map behavior has durable evidence;
- no non-Map gameplay action was sent;
- repository checks pass;
- all coherent work is committed and pushed;
- the final handoff states exactly what is ready for the next live/provider
  Goal.

Do not stop because one interesting contract was recovered. Finish the complete
Map subsystem goal above or reach a documented hard blocker.

When complete, stop. Do not automatically begin another subsystem.
