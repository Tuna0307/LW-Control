# R8-084 — recover native-capture hook routing and queue identity

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** safe-region native world-capture hook installer, detours, full-record producers and removal admission. No production scanner change.

## Result

Both verified embedded proxies install the same ten world-capture hooks:

- WorldPointManager.AddPointInfo
- WorldPointManager.RemovePointInfo
- WorldTileInfo.RemovePointInfo
- WorldPointManager.ParseWorldPointRemove
- WorldPointManager.ParseWorldPointFoldUp
- WorldMarchDataManager.AddMarch
- WorldMarchDataManager.UpdateMarch
- WorldMarchDataManager.AddOrUpdateMarch
- WorldMarchDataManager.TryRemoveMarch
- WorldTroopManager.UpdateTroop

Secure/plain code RVAs are identical. Their data/trampoline tables differ by exactly 0x1000, as expected from the two embedded images.
## Full-record producer routing

Point add capture is post-original: the AddPointInfo detour calls the original trampoline first and then tail-jumps to point producer `0x34CC0`.

March add/update capture is also post-original. AddMarch, UpdateMarch, AddOrUpdateMarch and UpdateTroop each call their original trampoline first and then tail-jump to shared march producer `0x34090`.

The point producer reads resolved `PointInfo.pointIndex`, `mainIndex` and `pointType` offsets directly. It rejects non-positive pointIndex, falls back mainIndex to pointIndex when mainIndex is non-positive, computes `isMainPoint` from equality, and only continues full-record capture for the canonical/main point.

Pending point records are keyed by that normalized mainIndex. Pending march records require a positive first captured qword and use the same qword for hash/equality; R8-083 establishes that first march field as `uuid`, so pending march records are keyed by captured UUID.

Both full-record queues replace an already-pending record with the same identity rather than blindly appending duplicates.
## Removal routing

Direct WorldPointManager.RemovePointInfo and WorldTileInfo.RemovePointInfo call the original first, then feed the recovered positive point ID to shared point-removal helper `0x39F20`.

ParseWorldPointRemove and ParseWorldPointFoldUp take the opposite ordering: their detours call parser helper `0x361A0` before tail-calling the original handler. That helper requests the exact SFS key `pointIds`, obtains its array, loops `get_Count`, calls `GetInt(index)`, and forwards each positive integer to `0x39F20`.

TryRemoveMarch calls the original first, then processes its positive qword removal identity in the native march-removal queue.

All producer paths compare the aggregate pending-record count against `0x10000` (65,536). When the ceiling is reached, replacement of an already-pending identity remains admissible; a genuinely new entry enters dropped-record helper `0x3A210`.

## Resolved game-facing inputs

The installer resolves exact field offsets for `PointInfo.pointIndex`, `PointInfo.mainIndex`, `PointInfo.pointType`, `WorldMarch._uuid` and `WorldMarch.pointIndex`.

It also resolves callable pointers for `GetResType`, `GetResLevel`, `GetWorldTreasureType`, `get_configId`, `GetMarchCurPosIndex`, `GetMaxHP`, `IsMonsterOrOrdinaryBoss`, `IsOrdinaryBoss`, `IsNormalType`, `GetSFSArray`, `get_Count` and `GetInt`.
These resolver names are source-backed producer inputs. They do not by themselves prove the final host-normalized meaning of every field; R8-083 remains authoritative for the native capture serialization contract.

## Boundary and remaining gaps

This checkpoint does not inspect or cross protected native region `0x3F8E0-0x40A6D`.

It does not recover `XluaBridgeMapScanTick` block traversal/order/coordinates, mode-specific per-tick work/request pacing, retry/backoff, or any separate acknowledgement semantics inside that protected layer. Those remain the central strict-parity gap.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_native_capture_hooks.py`

Durable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-084-native-capture-hooks.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner behavior is changed.
