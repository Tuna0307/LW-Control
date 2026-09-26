# R8-088 — recover native-capture point population

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** safe-region point producer field population and class/list transforms. No production scanner change.

## Result

Both verified embedded proxies resolve the same fixed 50-field IL2CPP offset bundle for point capture. Combined with the three core PointInfo identity fields from R8-084 and the four point-side getter routes from R8-086, the safe producer now source-accounts for all 57 fields serialized by the R8-083 point record.

The 50 raw fields are:

`ownerUid`, `uuid`, `serverId`, `srcServerId`, `worldId`, `itemId`, `id`, `level`, `quality`, `state`, `curHp`, `curMaxHp`, `allianceId`, `playerName`, `alAbbr`, `name`, `power`, `specialType`, `protectEndTime`, `shieldEndTime`, `completionTime`, `cfgId`, `stealList`, `expiredTime`, `taskExpireTime`, `actEndTime`, `gatherMarchUuid`, `gatherUid`, `gatherAllianceId`, `lastHpTime`, `unavailableTime`, `recoverSpeed`, `fireSpeed`, `heroList`, `rewarded`, `memberList`, `ownerServer`, `eventId`, `allianceAbbr`, `rewardUserList`, `diggingUserList`, `complete`, `ownerName`, `startTime`, `expireTime`, `createTime`, `type`, `fromPoint`, `multiple`, and `killerId`.
## Core identity and direct routes

R8-084 remains authoritative for point identity: positive `pointIndex`, non-positive `mainIndex` fallback to `pointIndex`, `isMainPoint` from equality, and full capture only for the canonical/main point.

The raw point bundle directly supplies the nullable server/world/state/HP/config/id/item/level/quality/rewarded/owner-server/from-point/multiple scalars and the qword-valued identity/power/time fields.

Three time fields have an exact width conversion before entering the R8-083 nullable-i64 slots:

- `protectEndTime`: int32 sign-extended to i64
- `shieldEndTime`: int32 sign-extended to i64
- `lastHpTime`: int32 sign-extended to i64

`recoverSpeed` and `fireSpeed` are direct nullable float32 values.
## Strings and runtime class

Eleven raw string fields pass through the shared IL2CPP-string conversion path before entering the exact R8-083 native string slots:

`ownerUid`, `allianceId`, `playerName`, `alAbbr`, `name`, `gatherUid`, `gatherAllianceId`, `eventId`, `allianceAbbr`, `ownerName`, and `killerId`.

`runtimeClass` is not a guessed taxonomy. The producer calls the resolved `il2cpp_object_get_class` on the captured point object, then `il2cpp_class_get_name`, and converts that exact runtime class name into the point record.

`isSpecial` is derived from a present raw `specialType` field by testing `specialType != 0`. The raw `complete` byte becomes the nullable point `complete` boolean.
## List-derived counts

Shared helper `0x3A080` resolves exact runtime field `_size` on the referenced collection object. Therefore these five point fields are exact native list-size projections:

- `stealList._size -> stolenCount`
- `heroList._size -> heroCount`
- `memberList._size -> memberCount`
- `rewardUserList._size -> rewardedCount`
- `diggingUserList._size -> diggingCount`

This is a source-backed `List<T>._size` read, not a host-side recount or inferred collection length.
## Class-specific branches

For exact runtime class `TreasurePointInfo`, the producer reads raw `startTime`, `expireTime`, and `createTime`, and populates `treasureType` from `GetWorldTreasureType` when that getter is available. If the getter is unavailable, raw `type` is the fallback source for `treasureType`.

For exact runtime class `WorldSuppliesPoint`, the ordinary raw `cfgId` route exists first; when resolved `get_configId` is available, its return value overwrites the point `cfgId` value/presence pair.

For `pointType == 7`, R8-086's `GetResType` and `GetResLevel` routes populate `resType` and `resLevel`.
## Boundary and remaining gaps

R8-088 closes safe-region point population. Together with R8-087, both full-record producer population models are now recovered outside the protected layer.

This checkpoint does not inspect or cross protected native region `0x3F8E0-0x40A6D`. It does not recover `XluaBridgeMapScanTick` block traversal/order/coordinates, mode-specific per-tick request/work pacing, retry/backoff, or any separate protected acknowledgement behavior.

## Verification

Hash-gated verifier: `tools/inspect_lwbridge_native_capture_point_population.py`

Durable evidence: `evidence/lwbridge-implementation/2026-09-27-r8-088-native-capture-point-population.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner behavior is changed.
