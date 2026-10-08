# Background World/Resource 004 — checkpoint A

Starting reviewed HEAD: `80e4f4d746333ca610b2f9180eb41a582e747e2f`.
Scope: checks-only runner / source-backed provider Start. No product
semantics, Lua auth, direct delegate, adapter binary, login or OS UI modified.

## Premature gate repaired

The old `tests/LWBridge.Desktop.Checks/BackgroundHomeMapWitness002.cs`
returned `BLOCKED_WORLD_OR_LOGIN_READINESS` if the separately-observed
status had `isInWorld=false`, without calling `map_scan_start`.

Production source contradicts treating this city state as fatal:
- `src/LWBridge.Map-0.3.17/MapScanStateMachine.cs:147-191`:
  authenticated current context -> if city, `EnterWorldMapAsync` ->
  require live/owned server and positive dimensions -> actual provider scan start.
- `src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs:31-90`:
  actual current-client Map status versus game-ready world-entry source.
- `src/LWBridge.Desktop/CurrentClientMapBlockSource.WorldReady.cs:25-35,247-350`:
  active launch session, identity-bound world-ready file/result and validation
  of profile/session/challenge/PID, world/server/dimensions, and cancellation.
- `tools/current_overview_bridge.lua:1092-1130`:
  already `WorldScene` or existing `SceneUtils.ChangeToWorld(callback)`;
  no extra UI clicks or invented scene command.

The corrected checks-only runner now admits only genuinely authenticated
owned profile/session/PID and positive live current server, then calls the
existing `Map317CommandService.InvokeAsync("map_scan_start", ...)`.
It **does not** substitute a world-ready result; failed/stale world readiness
still surfaces from the real provider and keeps run creation blocked.
It records a challenge-free world-ready summary with exact session/PID matches.

The old first-successful-block break/automatic Stop was also removed from
the completion attempt; the runner waits for the real terminal state and
examines durable run status, completed/failed blocks, staged-versus-published
data and MapStore queries. A separate explicit `--active-stop` mode is
available for exactly-owned active run cancellation evidence. It never
calls a cancelled staging result "completed".

Fixed three test-runner `map_search` payloads to use the actual
`{kind:"resource",query:{serverId,page,pageSize,...}}` contract.
The first live attempt, launched before that fix, completed the entire
real Resource run but its *test-only follow-up query* threw
`INVALID_MAP_QUERY` ("map_search query must be an object").
This was not a product Map defect and did not change published records.

## Inverses and real boundary

`tests/LWBridge.Desktop.Checks/BackgroundWorldResource004Checks.cs` runs
six explicitly **inert** inverses against the **actual production**
`MapScanStateMachine.StartAsync` with isolated
`MapProviderAdapter`: initially-city entry succeeds; already-world
bypasses entry; entry failure blocks provider start; stale connection blocks
run; cancellation does not start a run; replaced session fails closed.
Existing actual current-client world-ready source ownership tests and
native Map checks remain in their broader applicable suites.

Real completed attempt 001 was independently witnessed:
`live-attempt-001.json`: authenticated host/RPC, initially observed city
status; production `world-ready-result.json` **proven** with
`method=already_world_scene`, matched profile/session/PID, server **2212**,
worldId **0**, dimensions **1000x1000**; fresh Resource run
`b439bfa57cd54405a14e803d21964c9d`,
2500/2500 completed, zero failed. The already-world method does NOT claim
that a `ChangeToWorld` callback was actually invoked in that run.
Separate `completed-reopen-proof-001.json` performs a real read-only
reopen with `MapStore.ReadScanRun/Search`: **8008** live published
Resource rows; two different 50-row pages; impossible-keyword zero.
No historical City rows reused. The original installed script triplet
and journal are exactly restored after owned Home Stop.

Status: checkpoint A ready for lead review. Checkpoint B will distinguish
a second owned active Stop from the completed-run publication.
