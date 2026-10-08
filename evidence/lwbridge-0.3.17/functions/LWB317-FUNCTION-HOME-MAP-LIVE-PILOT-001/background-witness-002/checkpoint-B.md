# Background Witness 002 — checkpoint B: actual game and native boundary

Date: 2026-10-08 (Singapore/UTC+08). Parent pilot status: **PARTIAL**.
This file documents what was actually witnessed. It is not an original LWBridge
0.3.17 A-to-A or canonical UI proof.

## Pre-launch safety, identity and isolation

- Started from clean `research/offline-controller` / `876ba787`, then
  checkpoint A `2ca93838d1de2a4755a8ebe34d515dbf4b8b19aa`.
- No LastWar, launcher or clone processes at each launch admission.
- Current native client admission and `tools/check_current_client_runtime_contract.py`
  passed: binary identity approved, version 22, critical Lua entries unchanged.
- Historical attempt-5 `overview-bridge/recovery.json` absent.
- Every attempt creates a fresh `LWB317-BACKGROUND-WITNESS-002-<guid>`
  root under system Temp and unique profile. Production host/map service
  composition and launches run there with Auto disabled. No fixture provider.
- Immediately before each launch, verified LocalLow source scripts against:
  `LWScripts.data` SHA256 `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`;
  `LWScripts.txt` `d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a`;
  `version.txt` `785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09`.
  Independent original copies and helper's validated manifest backups retained
  under each isolated attempt's `overview-bridge-backups`.
- No desktop screenshots, mouse, keyboard, focus, desktop UI control, outside
  Lua probes, original protected services or unrelated gameplay operations.

## Live attempt 001 — fresh negative host witness

`live-attempt-001.json`:
- Isolated profile: `lwb317-background-witness-002-6f50f14b`;
  instance `a2a2e32065844fb3a91d34c959fe0acf`.
- Process identity: PID `63552`, game process started
  `2026-10-08T00:51:30.6092933Z`. Real Home start returned
  `2026-10-08T00:51:49.303532Z`.
- First immediate host observation: `bridgeConnected=false`,
  `connectionState=reconnecting`; authenticated sessions `0`,
  routes `0`, rejected handshakes `0`, handshake error null.
- Game-side `overview-bridge/pipe-transport.json`, timestamped during
  launch, reports `adapterActive=true`, `adapterLoaded=true`,
  `state=hello_sent`, `clientConnected=false`.
- Classified `BLOCKED_NO_CORRELATED_HOST_ACK`. Never attempted
  `map_scan_start` or reused archived City/Resource records.
- Actual production `profile_instance_stop` exact owned instance succeeded.
  Helper manifest restored, journal absent and three originals byte-identical.

This immediate-only host observation could race with asynchronous adapter worker
startup. **That is a test-owned observation gap, not a proven product fault.**
The test runner was then updated to observe 20 seconds with listener and
game-adapter diagnostics before classifying failure.

## Live attempt 002 — bounded correlated host diagnosis

`live-attempt-002.json`:
- Distinct profile `lwb317-background-witness-002-3a9a1a99`,
  distinct live instance and game-process identity (see archived JSON).
- Actual `OverviewLifecycleService` launch and production helper reached
  Home `ready`. No native pipe authenticated route was observed.
- **28 samples over 20 seconds** (`hostTrace`):
  - Host native server instance count `1`, initial disposition `Pending`,
    Win32 `ERROR_IO_PENDING=997`, which indicates an overlapped pending
    connect, **not** a handshake failure.
  - Authenticated sessions `0`, connected instance routes `0`,
    handshake rejects `0`, failed native connects `0`.
  - Host connection state stayed `reconnecting`; profile/session/PID
    and fresh heartbeat were not enough to satisfy actual host authentication.
  - No adapter state file found within isolated `overview-bridge` during
    the bounded samples. Game-side Lua diagnostic still
    `state=hello_sent`, `clientConnected=false`, adapter loaded/active.
- Classified `BLOCKED_NO_CORRELATED_HOST_ACK`, stopped only the
  exact owned session, restored scripts and cleared recovery journal.
- No Lua RPC result, no fresh Resource scan start/run ID, no captured block,
  no staged/published Resource row. Those absent outcomes are **not PASS**.

The missing link is **client adapter/native pipe connect and authenticated
hello ACK for the same new profile/instance/PID**. The evidence does not
establish whether the .NET game adapter never issued its native connect,
reported its state elsewhere, or failed to connect; host counters distinguish
this from a **rejected** hello, but do not prove root cause. No code change to
the product's host handshake, client adapter or provider is justified from
this trace alone. Source loci:
`tools/current_overview_bridge.lua:298-418,3715`,
`src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:52-181,298-313`,
`src/LWBridge.Desktop/LWBridgeControlPipeIsolatedAcceptLoop.cs:124-215`,
`src/LWBridge.Desktop/LWBridgeControlPipeIsolatedHandshake.cs:13-144`.

## Post-run independent audit and persistence negatives

Run the repeatable *read-only* archive auditor:
```powershell
python tools/lwbridge317/audit_background_witness_002.py --output evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-witness-002/live-attempts-audit.json evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-witness-002/live-attempt-001.json evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-witness-002/live-attempt-002.json
```

`live-attempts-audit.json`:
- BOTH independent pre-launch exact backups verified; BOTH helper manifests
  finished `restored` with matching hashes; BOTH owned Home Stop succeeded;
  BOTH local recovery journals absent, three installed script hashes restored.
- Fresh read-only SQLite reopen of each actual isolated profile database:
  `scan_runs=0`, `scan_blocks=0`, `resource scan_records=0`,
  `resource map_records=0` on **each** attempt. No inherited prior rows.
- Fresh authenticated host? **No**. Fresh Resource start/capture? **No**.
  This negative SQLite evidence must not be represented as new Resource
  query/filter/pagination/export/reopen success.
- Production `Map317CommandService` exposes City export only and its native
  save-dialog boundary cannot be used under no-desktop-input restrictions.
  Resource export is not a supported equivalent endpoint; no export claim.

## Regression and cleanup

`checkpoint-B-checks.txt` records exit code zero for:
`home_runtime_file_ownership_checks.py`, `home_runtime_lease_lua_checks.py`,
`--map317-native-boundary-check`,
`--overview-bridge-isolated-handshake-check`,
`--overview-status-transport-check`. Together with checkpoint A,
all applicable focused tests passed; isolated checks are **not native game
handshakes**. Release checks project builds with zero warnings/errors and
the normal app's option rules remain unchanged.

The installer scripts and historical pilot materials were not overwritten
outside the production helper's authorised exact-game launch/restore transaction.
No new product behavior, provider override, gameplay fallback or UI automation.

## Remaining prerequisites for lead/owner review

1. Diagnose why the **actual Last War** pipe adapter never reaches the
   host's pending named-pipe server. Obtain a session-correlated accepted hello
   and RPC result before enabling any real Resource acquisition.
2. Only after real connected host/world readiness is proven, run a new
   bounded current-server Resource scan through `Map317CommandService`,
   preserve fresh run ID and request IDs, capture, Stop, stage/publish and
   verify actual query/filter/page/reopen. Resource export would need a
   separately legitimate production endpoint; no dialog automation.
3. Canonical Home/Map UI visual/click/manual Stop/reopen proof still needs
   separately authorised owner foreground interaction. Original controller
   plaintext remains blocked by legitimate unavailable decryption inputs,
   outside this work item.

Checkpoint B is complete as a **fresh negative native transport witness and
exact restoration**, not as completed Resource/Map acceptance.
Status: **AWAITING_REVIEW**.
