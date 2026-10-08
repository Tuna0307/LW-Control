# BACKGROUND-CONTINUATION-001 — archived Resource and pipe diagnosis

Date: 2026-10-08. Starting HEAD:
`3634daa30a036eaaa567553dea44e2aed65e4f71`.
Permission: BACKGROUND ONLY, no screenshots, desktop input/focus, or UI-dependent
live claims. All seven attempt-5 snapshots preserved **unchanged**.

## Timestamped separate-run reconstruction (all UTC)

Source: `attempt-5-fresh-ready/map-pre-scan.json`,
`processes.json`, `ready.json`, `heartbeat.json`, and `recovery-active.json`.

| UTC time | Observation | Attribution |
| --- | --- | --- |
| 2026-10-07 14:32:49.279 → 14:34:41.140 | City run `efe454e8a9ea4b0aa09e027dd625e273` completed 2500/2500; older 7000 City rows | **Prior run**, not attempt 5 |
| 2026-10-07 14:35:53.075 → 14:38:10.362 | Resource run `c04110e9cef44453bd79b3f61276c8a6`: `cancelled`, 2500 total, 0 completed, 0 failed, 0 Resource rows, no stored error | **Prior run**, closed ~78 minutes **before** attempt-5 game was started |
| 2026-10-07 15:51:51.015 | `LWBridge.Desktop.exe` PID 36092 created (captured process inventory) | Attempt-5 host observation only |
| 2026-10-07 15:56:08.347 | `LastWar.exe` PID 66036 created | Attempt-5 game |
| 2026-10-07 15:56:18 | `ready.json` readyAt; recovery record for PID 66036/session `1ba5a00e5c8646dd81341f7beb736d24` | Owned game bridge readiness observation |
| 2026-10-07 16:04:51 | `heartbeat.json` updatedAt: connected=true, ready=true, gameReady=true on server 2212 | In-game heartbeat, **not** authenticated host pipe result |
| 2026-10-07 16:04:51.459 | `map-pre-scan.json` capturedAt | Snapshot of persisted **earlier** scan runs |

The saved `pipe-transport.json` reports
`adapterActive=true, adapterReadMode=direct, state=hello_sent,
clientConnected=false`. It does **not** include a timestamp, profile,
session, request, host acknowledgment or correlated RPC result, so its instant
cannot be safely ordered relative to the heartbeat. The difference is **not a
demonstrated defect**; `connected` in game heartbeat and transport
`clientConnected` are separate variables and readiness contracts.

## Actual production source path and ownership

- Host launch and owned readiness: `src/LWBridge.Desktop/OverviewLifecycleService.cs`
  `IsSnapshotReady` ~1375–1393 checks owned PID, start time/path, heartbeat
  identity/freshness. `src/LWBridge.Desktop/OverviewMapScanSession.cs`
  10–31, 79–118 binds the Map run to profile/session/challenge/PID/path/start
  and waits for fresh healthy in-game heartbeat. Neither alone is proof of
  an authenticated pipe RPC.
- Lua bridge initialization: `tools/current_overview_bridge.lua`
  `ensure_pipe_hello` ~324–418 checks environment profile, instance, build,
  token length and paths, starts native adapter, then sets
  `adapterActive=true; clientConnected=false; state=hello_sent`.
  `refresh_pipe_adapter_state` ~296–320 may report adapter state;
  `process_pipe_inbound` ~3421–3455 transitions to `connected` on validated
  `hello.ack`; `pipe_transport_capabilities` ~135–173 writes a **transport**
  diagnostic. The normal `observe_game_connection` and heartbeat publication
  are distinct from that transition.
- Native control pipe: `src/LWBridge.Desktop/LWBridgeControlPipeIsolatedHandshake.cs`
  14–137 reads framed hello with the handshake deadline, validates PID,
  build, executable path and registered token, then writes `hello.ack`.
  `LWBridgeControlPipeRpcSessionTransport.cs` 192–329 correlates the
  authenticated profile/instance on result frames and separately records
  heartbeats. No host acceptance/correlation witness was preserved for attempt 5.
- Production admission/acquisition: `CurrentClientMap317ScanProvider.cs`
  70–119 rechecks current world/server and activates only accepted run IDs;
  121–176 wires `MapScanEngine` to a run-scoped sink and cancellation;
  201–232 retires the engine/lease on terminalization.
  `Map317CommandService.cs` 477–516 inserts/registers accepted durable runs
  before activating the provider and holds the lease; 519–532 serializes Stop.
  `CurrentClientMapBlockSource.WorldReady.cs` 25–34 requires healthy same
  session and world readiness; `CurrentClientMapBlockSource.FastCity.cs`
  28–40 and 725–795 define bounded Resource batch request/correlation and
  cancellation/timeout via the actual game bridge.
- State/DB semantics: `src/LWBridge.Map-0.3.17/MapControlPlane.cs`
  38–79 persists a run and progress; `MapScanStateMachine.cs` 345–404
  persists local cancellation then best-effort provider Stop; no success is
  fabricated. `MapScanEngine.cs` 28–129 increments completed only after
  successful capture and checkpoints; cancellation goes to `sink.Stop`.
  Therefore zero completed blocks can coexist with an in-flight/awaiting first
  block when Stop cancels; these archived rows do not prove which wait, if any,
  failed. `CurrentClientMap317ScanProvider.cs` 222–276 stages only
  returned valid records, then publishes upon completed scan.

## Finding, counterfactual and next missing proof

The first genuinely **unproven** attempt-5 boundary is a timestamped,
session-bound authenticated host `hello.ack` / RPC result, followed by an
attempt-5 **new** Resource run/capture and terminal result. Neither is in
the seven archived files. The older cancelled Resource run cannot be assigned to
the later PID. An empty Resource population alone does not prove zero resources
in-game or a provider defect.

Executable check `tools/lwbridge317/check_background_pilot_readiness.py`
reads these exact evidence files; five isolated inverse tests under
`tests/background_pilot_readiness_checks.py` deliberately shift a Resource
run into the later process window, flip pipe state and reject identity/time
mismatches. Tests classify archived evidence and **are not live transport
oracle tests**. Existing production C# transport/Map tests are run separately.

No supported in-scope code defect is demonstrated by these saved snapshots.
No provider, protocol, lease/Stop/profile fence or A-to-A behavior was changed
on speculation. Until the owner explicitly resumes foreground interaction,
UI-driven Resource Start/Stop and canonical Map reopen/export remain pending;
headless-only tests cannot be called LIVE_PROVEN. No game was launched, no
installed scripts were mutated, and no owner data or saved attempts were removed
in this continuation.
