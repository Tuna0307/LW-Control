# Background Pipe Connect 003 — checkpoint B: real-game retests and stop

Disposition: **PIPE CONNECT FIX LIVE-PROVEN; Resource BLOCKED by world scene**.
Fresh current-client background tests only; **not canonical UI or original 0.3.17
behavioral parity**. Owner expressly allowed game launch, but not desktop
screenshots, keyboard, mouse or focus interaction. None was performed.

## Exact live sequence

Preflight each time with fresh isolated per-attempt profile, current-game
admission and original-script byte checks. The runner calls production
`ProfileRuntimeOwner.Create(mapProvider:null)`, native host transport,
`OverviewLifecycleService.profile_instance_start/stop`,
`Map317CommandService` and real current-client Map provider.
Auto disabled, default owner's state not selected. Saved originals backed
up independently before instrumented launcher transactions.

| Attempt | Adapter route / native evidence | Host / provider |
|---|---|---|
| 001 | Reflected `MethodInfo.Invoke`, userdata, `pcall_returned`; no native state file | 0 authenticated, 0 routes, `BLOCKED_NO_CORRELATED_HOST_ACK` |
| 002 | Direct userdata call once, adapter state connected; game pipe state connected | 1 authenticated, 1 route, production current-server Map status `serverId=2212,isInWorld=false` |
| 003 | Direct once; same PID/session native entry → worker → WaitNamedPipe → open → hello frame written; state connected | 1 authenticated, 1 route, 0 rejects; genuine production `getStatus` RPC returned a non-null Object, no pending calls; current-server Map `serverId=2212,isInWorld=false` |

Authoritative files:
`diagnostic-attempt-001.json`, `retest-attempt-002.json`,
`rpc-retest-attempt-003.json`, `receipts-001/`, `receipts-002/`,
`receipts-003/`, and independent `read-only-audit.json`.
The bridge host's ACK is not inferred merely from `connected` adapter
state. It is separately corroborated by authenticated count 1,
connected route 1, fresh profile/instance/PID ownership and Lua's
`pipe-transport.json state=connected` after inbound processing.
The *real* `getStatus` returns via the production
`LWBridgeControlPipeHostState.CallLuaAsync` / frame registry and
game-side Lua result path, rather than an inert fabricated result.

## Resource gate

Each fresh real Map context explicitly reports the connected login/server
(`serverId=2212`) but `isInWorld=false`; the world map scene is not
active. Therefore the production runner correctly does **not** invoke
`map_scan_start` and cannot claim a new Resource run, block/result,
staging, publication, filter, pagination or reopen of positive Resource
data. The reused 7,000 historical City rows were not counted.
Existing Resource exporter is City-only; no Resource export invented.

The exact **external/UI prerequisite** is a legitimate city-to-world
transition in the current session; the background-only desktop restriction
prohibits input/focus automation to cause it. No gameplay movement,
cross-server commands, updater, plunder, claims, spending, protected services
or unsupported auto-map provider actions were attempted.

## Stop, restoration, and ownership

All three attempts used the production exact-owned
`profile_instance_stop`, not a generic process kill or a restoration
underneath running Last War. Each reports
`ownedGameStopSucceeded=true`, `originalHashesRestored=true`,
`recoveryJournalAfter=false`, exact helper manifest state `restored`.
The read-only auditor reopens every **actual isolated SQLite** file:
`scanRuns=0,scanBlocks=0,resourceScanRecords=0,resourcePublishedRows=0`
for each fresh attempt. Original `LWScripts.data`, `LWScripts.txt`,
`version.txt` hashes remain exact. Final process inventory: no
running LastWar, launcher or LWBridge clone.

`tools/lwbridge317/audit_pipe_connect_003.py` fail-closes against
mismatched Lua session/PID, route, native stage, host identity, RPC result,
nonempty scan database and failed restoration. Its output is
`read-only-audit.json`.

## Review boundary

- Completed: Lua userdata-native entry diagnosis; actual live negative
  and positive comparison; direct invocation source fix; opt-in native receipts;
  successful actual host authenticated ACK; actual production `getStatus`
  RPC; world scene negative; owned Stop and exact restoration.
- Unproven: any Resource scan/capture/publication/stop within a new run;
  current-world maps/UI click/manual Stop/reopen; original encrypted protected
  Lua parity. Do **not** upgrade full pilot to LIVE_PROVEN.
- Next owner-assisted step, only after renewed desktop control is explicitly
  allowed: transition to world view in an isolated owned session; then
  run source-backed current-server bounded Resource scan, correlated run/lease,
  capture, completed versus canceled terminal status, durable rows,
  query/filter/page/reopen. Stop only exact owned run/session, restore originals.

Status: **AWAITING_REVIEW** with Resource dependent branch blocked by
actual `isInWorld=false`/no-desktop-input gate.
