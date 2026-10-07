# LWB317 live pilot — checkpoints B/C ownership boundary

Date: 2026-10-07
Branch: `research/offline-controller`

## Status

Checkpoint B cannot be completed against the currently running game session without
violating the pilot's explicit ownership rule. The worker therefore did not issue
Home Launch Game, Map Start Scan, Stop, game close, or restoration commands against
that session.

Checkpoint C's local-only storage/query/export slice was completed on a read-only
snapshot of the isolated Map database. That local verification is not counted as a
B live result.

## Session ownership evidence

At B preflight there was no `LastWar.exe` or `LastWarLauncher.exe` process.
The worker recorded the exact installed v22 game/script baseline before any live
mutation.

During later Windows-MCP interaction, `USER_CONTROL` / `CONTROL_PREEMPTED`
events showed that the owner had physical control of the desktop. A new Home/game
pair then existed that the worker had not launched:

- `LWBridge.Desktop.exe` PID 63052, created 2026-10-07 22:23:45 +08:00.
- `LastWar.exe` PID 40992, created 2026-10-07 22:26:09 +08:00.

The worker had not clicked Home's Launch Game or Map's Start Scan before those
processes appeared. The current Map UI also showed persisted City data timestamped
around 22:34 and a completed scan interval around 22:35–22:38. Those rows are
therefore not claimed as this worker's live pilot acquisition.

See `checkpoint-b-session-lineage.json`.

## Exact recovery / restoration state

The isolated recovery record currently names:

- profile: `lwb317-live-pilot-001`
- game PID: 40992
- stage: `active_ready_deferred_restore`
- exact backup:
  `C:\Users\chimw\AppData\Local\Temp\LWB317-LIVE-PILOT-001-root\overview-bridge-backups\20261007-142602-46861c0ac9f04f69a1e83301cb0c781c`

It records the exact original script triplet:

- `LWScripts.data` SHA-256
  `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`
- `LWScripts.txt` SHA-256
  `d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a`
- `version.txt` SHA-256
  `785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09`

Read-only hashing while PID 40992 is active shows the candidate script package is
still installed:

- current `LWScripts.data`:
  `54ef1dd91ee093558b15f894573fcfee4caa46dcfcd134c139ea4aa5c17e8c7c`
- current `LWScripts.txt`:
  `e69b1d9b0c7459bb94144c2286301902fa78e4fd331fa201ddfad987bfd96baa`
- `version.txt`, `LastWar.exe`, `xlua.dll` and `Assembly-CSharp.rdl`
  still match the preflight baseline.

Because PID 40992 is not worker-owned, the worker did not close it and did not
restore files underneath a running foreign session. Exact byte restoration is
therefore still pending the ownership-safe Stop/cleanup continuation.

See `checkpoint-b-owner-session-recovery.json` and
`checkpoint-b-current-game-file-state.json`.

## Production-data isolation during the active session

The normal owner data root
`C:\Users\chimw\AppData\Local\LWBridgeRebuild` still has the exact same
metadata digest recorded before live testing:

`cfe79dee154505b21387e27a6f40083c939f3e51b99e3f037ce022c39e6b4946`

This remained unchanged while the isolated session had active Overview recovery,
which proves the Python/Lua helper root propagation is not writing to the normal
owner LWBridge root.

See `checkpoint-b-owner-root-prelive.json` and
`checkpoint-b-owner-root-during-owner-session.json`.

## C local-only verification

A new deterministic check snapshots the live pilot Map SQLite database with
SQLite's read-only backup API before testing, so the active source database is
not modified.

Observed local persisted state for server 2212:

- City rows: 7,000
- Resource rows: 0
- pagination: page size 50; page 1 = 50; page 2 = 50; no overlap
- seven City filter contract checks passed over the full 7,000-row population
- database close/reopen preserved City/Resource counts
- City workbook export wrote 7,000 rows
- reopening the generated XLSX found exactly 7,000 data rows

The workbook is
`checkpoint-c-local-city-export.xlsx`.

This proves local filtering/pagination/export/reopen behavior against persisted
pilot data, but does not prove a worker-owned live scan. In particular, Resource
remains unproved live because the persisted Resource population is zero.

## Concurrent descendant work preserved

While the pilot was active the branch advanced with descendant commits
`d2750f21`, `32d6fa2a` and `4fbe0c10`, plus unstaged changes in
`tests/home_runtime_lease_lua_checks.py` and
`tools/current_overview_bridge.lua`. These were not created by this worker in
this execution path. They were not reset, overwritten or staged.

The descendant commits contain additional live transport integration attempts and
fixes. Their evidence is preserved, including
`checkpoint-b-attempt-001`. This record does not relabel those attempts as this
worker's owned session.

## Remaining B/C requirements

Still required for a complete live pilot:

1. Wait for the owner-operated game session to end without this worker closing it.
2. Confirm exact script-triplet restoration to the preflight hashes and clear
   recovery state.
3. Re-preflight current-client compatibility and session ownership.
4. Launch a fresh worker-owned session from canonical Home.
5. Prove same-session xLua binding/readiness for that owned PID/session.
6. Run bounded current-server Manual City and Resource scans through actual Map
   controls. Do not enable recurring Auto scan.
7. Verify Stop on that owned scan/session.
8. Verify local filters/pagination/export and persisted reopen for the worker-owned
   results.
9. Close only the worker-owned game session and prove exact restoration/cleanup.

Original Lua decryption remains a separate missing-input dependency and is not
part of this live-pilot blocker.

No claim/plunder/share/march/combat/spending/cross-server movement/recurring Auto
scan/updater action/protected-original service access or login automation was
performed by this worker.
