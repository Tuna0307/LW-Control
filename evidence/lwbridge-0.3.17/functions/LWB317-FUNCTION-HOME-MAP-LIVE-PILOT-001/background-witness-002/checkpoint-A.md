# Background Witness 002 — checkpoint A (isolated actual-provider runner)

Source checkout: `876ba787ad4d5e01b633c7a451a492814f04fd32`, branch `research/offline-controller`.
This is **not a live game / original 0.3.17 parity witness**.

## Exact test composition

Entry points are only in `tests/LWBridge.Desktop.Checks/Program.cs`:
`--background-witness-002-self-test` (no game),
`--background-witness-002 <output.json>` (isolated preflight),
and `--background-witness-002 <output.json> --launch` (bounded real-game operation).
The ordinary desktop app's Program option gates and defaults are unchanged.

The runner calls real `ProfileRuntimeOwner.Create` with a new explicit
`DesktopApplicationPaths.Create(temp)` root, per-attempt profile/config/registry,
`mapProvider:null`, real current-client game-root admission, real native pipe
listener, Overview lifecycle and `Map317CommandService(lifecycle)`.
`startAutoScheduler:false`, `AutoLaunchGame:false`,
`AutoReconnect:false`; no UI input/capture, fixtures, or original protected service.
The installed LocalLow `lwScripts` original triplet is hash-checked against the lead
checkpoint and independently backed up into the attempt's isolated root
*before* any launch; backups and all attempts are retained.

Production source chain: `ProfileRuntimeOwner.cs:79-208` →
`OverviewLifecycleService.cs:612-790` and
`OverviewLifecycleProfileInstanceStatus.cs:15-90` →
`LWBridgeControlPipeHostState.cs:40-98,195-350` →
`Map317CommandService.cs:47-76,450-555` →
`CurrentClientMap317ScanProvider.cs:25-160`.
A host acceptance observation requires actual host authenticated-session count,
connected instance route, exact profile/session/PID, fresh heartbeat and
`connectionState=connected`; no conclusion from a game heartbeat alone.
The runner also checks the real spawned game's canonical executable path and
records process start timestamp. Its protocol artifact summaries whitelist
requestId, status, session/profile/PID matches, time and errors—no token,
challenge, or Lua command body.

## Inverse and preflight results

- Release checks project build (no product source edit): PASS, 0 warnings / errors.
- `--background-witness-002-self-test`: **7/7 PASS**, *inert classifier seam only*.
- Actual production host transport / lifecycle launch binding / RPC session /
  global call registry / isolated profile-root boundary checks:
  **5/5 PASS**. Raw per-case exit, `ok` and elapsed seconds:
  `checkpoint-A-checks.txt`. These tests use isolated actual host boundaries,
  but synthetic handshakes/clients; **not** a fresh Last War handshake.
- `isolated-preflight-001.json` and `002.json`:
  initial runner sequencing defect (new root created by independent backup
  before duplicate root-fresh assertion). Preserved as failed attempts; neither
  started a game, all three original hashes remained exact and no recovery
  journal was present. Fixed test-only code (no product diagnosis).
- `isolated-preflight-003.json`: **PREFLIGHT_ONLY_NO_GAME**. Actual
  `ProfileRuntimeOwner.Create` real provider composition and native listener,
  stale `profile_instance_stop` → `INSTANCE_NOT_OWNED`; disconnected real
  `map_scan_start` → `GAME_CONNECTION_UNAVAILABLE`.
  Three script SHA hashes exact, recovery journal false, no running pilot game.

The existing normal `Program.cs` denies combining `--isolated-root` and other
proof modes. This runner has its own checks-only explicit entry point and
does not weaken those startup rules.

## Next checkpoint

Execute `--launch` only following fresh process/journal/hash and current build
admission. Try real session-bound hello/ack, world readiness and Resource run;
record exact block reason if login/world requires UI. Stop only exactly owned
session/run and verify restoration. Resource export is not a current
`Map317CommandService` operation (`map_city_export` is City-only and requires
desktop save dialog); do not misstate absence as a Resource export success.
