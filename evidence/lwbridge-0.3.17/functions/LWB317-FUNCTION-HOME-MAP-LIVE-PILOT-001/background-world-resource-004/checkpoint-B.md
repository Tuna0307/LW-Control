# Background World/Resource 004 — checkpoint B

Date: 2026-10-08, source branch `research/offline-controller`.
Outcome: **REAL SAME-SERVER RESOURCE COMPLETION AND ACTIVE STOP PROVEN**,
subject to explicitly limited UI/original-parity and command-layer coverage.

## Scope and provenance

The owner permits native, session-owned Last War background launch and
existing same-server world entry but prohibits OS screenshot, keyboard,
mouse, focus/window activation. None was used.

The checkout started clean at `80e4f4d746333ca610b2f9180eb41a582e747e2f`.
A separate actor concurrently fast-forwarded the shared branch at
`df4c955127ddcf07f98858bf9f1e245da56bc328` with a source-backed
Lua authenticated-pipe heartbeat publication fix. It was **preserved**,
not authored/claimed by this 004 worker; independent heartbeat regression
ran on the combined checkout. While checkpoint A was being committed,
a second externally initiated checks-only pilot process was observed with
`--background-witness-002 .../live-active-stop-002.json --launch --active-stop`.
This worker did **not** spawn, control, or interfere with that concurrent
process. It was the exact already-authorized production test runner, with
fresh process/profile, isolated backup/root and native ownership fences.
Its result was independently inspected and audited after termination,
not retroactively described as launched by this worker. No third session
was launched to compete with it.

## Completed 001 — real, positive

`live-attempt-001.json` is the first launch from this 004 worker's
isolated production composition, still using the accepted single direct
userdata pipe Connect fix. Actual session-bound native host accepted
and responded to a real `getStatus` RPC. Initial Map status reported
`isInWorld=false`. The corrected runner invoked **the normal production**
`Map317CommandService.map_scan_start` instead of returning early.
The existing `MapScanStateMachine.StartAsync -> EnterWorldMapAsync ->
CurrentClientMapBlockSource.EnsureWorldReadyAsync` identity-bound path
returned a fresh correlated `world-ready-result`:

- method **`already_world_scene`**, state `proven`
- profile/session/game PID matched, server **2212**, worldId **0**
- tile dimensions **1000 x 1000**; no cross-server or invented UI action.

The original source's alternate
`SceneUtils.ChangeToWorld(callback)` transition exists but **was not
observed as invoked** in this particular live run. This run validates
the existing world-ready branch and accepted current-server scene;
it does not prove every city-transition mode.

Fresh Resource run ID `b439bfa57cd54405a14e803d21964c9d`,
types `[resource]`, mode `normal`, expected **2500** blocks.
Actual engine terminal state: **completed 2500/2500; 0 failed;
100%**. Actual isolated SQLite `scan_runs.status=completed`
and `map_records.kind=resource`: **8008 published records**, with
no previous City rows inherited and no surviving staging rows.

The first runner version sent an invalid *post-completion* query shape
(`INVALID_MAP_QUERY: map_search query must be an object`), classifying
its test-owned terminal `BLOCKED_LIVE_PREREQUISITE_OR_FAILURE` although
the durable Resource engine had already completed. The product's actual
Map data were **not** affected. The runner now uses the recovered
`{kind,query:{serverId,page,pageSize,...}}` envelope. Independent
`completed-reopen-proof-001.json` is from real production
`MapStore.ReadScanRun/Search` against the exact preserved SQLite database:
run status completed and counts 2500/2500/0; Resource total **8008**;
page 1 **50**, page 2 **50**, distinct record keys, and an impossible
keyword returns **0**. These rows are actual game-captured/published data,
not synthetic/inert row fixtures. The high-level `map_search` command
*on the positive completed session* was **not** independently proven
after the malformed first-run request; the production storage/search
implementation *was* verified by independent reopen. Do not conflate
these two distinct proof layers.

Owned Home Stop succeeded, helper restore manifest `restored`, no
pending journal, exact original script-triplet SHA256 matched.

## Active Stop 002 — separately negative publication

Independently observed and audited `live-active-stop-002.json`:
same legitimate native testing scope, **different fresh profile/instance/PID
and Temp root**. The second game run reached a new authenticated host/RPC,
correlated world-ready response and an accepted same-server Resource run:
`b9fc31a14f6f4c3d819c0e17efaddbe8`.

The recovered production Map Stop was issued while the exact owned run
had `isReading=true`, phase `scanning`, **one block in flight**,
zero completed blocks. The Stop response showed `isReading=false`,
phase `idle`, zero inflight; durable reopened run status **cancelled**,
total 2500, completed 0, failed 0. Production command
`map_search` with the corrected envelope returned zero records,
and independent reopened SQLite has 0 staged and 0 published Resource
rows. **This is an active-stop/cancelled staging outcome, NOT completed
positive publication.** It separately establishes actual Stop ownership
and terminalization; it does not claim stopping after any positive staged
record.

Exact owned Home Stop then completed; recovery journal removed and
all three originals restored. `tools/lwbridge317/audit_world_resource_004.py`
with both reports validates all manifests, original backup/postrun hashes,
world identity, full completed 8008-row publication versus independent
cancelled zero-row state, and Stop event provenance. Output:
`read-only-audit.json`. No game or clone remained running at final inventory.

## Regression and limits

Six `BackgroundWorldResource004Checks` inverses ran through the
**production** `MapScanStateMachine.StartAsync` and real adapter
boundary with explicitly inert hooks: initially-city success,
already-world no transition, failed entry, stale session, cancelled
entry and replaced session. Existing native Map, world-source,
authenticated pipe, source-only ownership/Lua/heartbeat suites,
Release/frontend and packaging checks are in `verification-004.txt`.
The production source's auth/lease/Stop semantics were not weakened.

This background test does **not** prove canonical Home/Map UI
click/visual/manual Stop/reopen acceptance or 0.3.17 original Lua
behavioral A-to-A parity. Resource export is **not** implemented;
the native recovered exporter is City-only with desktop dialog.
Do not invent Resource export evidence or compare to protected
original service.

**Remaining dependencies:** separately authorized foreground UI review;
further source-backed original 0.3.17 behavior if supplied; a future
ordinary city-to-world transition callback proof if needed (this
session returned `already_world_scene`). Parent pilot is **PARTIAL
for full original/UI acceptance**, despite positive current-client
native Resource completion and separate active Stop.

Status: **AWAITING_REVIEW** for independent lead acceptance.
