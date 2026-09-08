# LWBridge Map Scan recovery

Map Scan is the first feature to recover one-for-one.

## RECOVERED control path

Static xrefs tie `enterWorldMap` and `startMapScan` to `src\\services\\map_scan.rs`.

Observed behavior:

1. Reject missing game connection.
2. Reject when another map scan is already running.
3. Read world state (`isInWorld`, `homeServerId`, `seasonServerIds`).
4. Call `enterWorldMap` when needed and wait for world-map readiness.
5. Validate `scanMode` as `normal` or `fast`.
6. Validate selected scan types and map dimensions.
7. Start the scan with `startMapScan`.
8. Track direct block progress while native point/march hooks feed capture queues.
9. Commit/publish map records through the map-index layer.
10. Call `stopMapScan` and close the native capture session on stop/failure/completion cleanup.

Recovered progress/status names include:

`scanRunId`, `totalBlocks`, `completedBlocks`, `readBlocks`, `failedBlocks`, `unreadBlocks`, `inflightBlocks`, `scanRate`, `status`, `lastError`, `resumeAvailable`.

These fields strongly indicate the original design can distinguish successful reads from failed/unread blocks and has a resumable scan state. The rebuild should preserve that distinction instead of treating “loop finished” as success.

## RECOVERED scan modes

The mode branch is now resolved far enough to name the scheduler value exactly:

- `normal` uses `concurrency = 8`.
- `fast` uses `concurrency = 20`.

Static evidence in the Map Scan state machine stores `8` or `20` in the same state field, then later loads that field while serializing the `concurrency` property. The other mode-dependent constants previously seen beside them, `6` and `4`, are the byte lengths of the strings `normal` and `fast`; they are stored with the mode string pointer and later serialized as `scanMode`. They are not a second timing/concurrency setting.

Any additional mode-specific pacing, timeout or retry differences remain UNKNOWN until separately traced.

## RECOVERED selectedTypes contract

The exact accepted values are an eight-entry static allowlist:

1. `city`
2. `resource`
3. `monster`
4. `truck`
5. `railway`
6. `dispatch`
7. `ghost`
8. `treasure`

The allowlist is referenced by the filtering routine at RVA `0x20D26F`; the verified table is at RVA `0xC81908`. A second eight-entry table used by the fallback path at RVA `0x825D98` contains the same values in the same order.

Recovered filtering behavior:

- If `selectedTypes` is absent or is not an array, LWBridge defaults to all eight values above.
- Array entries must be strings and must exactly match one of the eight allowed values.
- Unknown and non-string entries are discarded.
- Duplicate accepted values are removed while preserving the first accepted occurrence.
- If filtering leaves zero valid types, the scan fails with `INVALID_SCAN_TYPES` / `no valid map scan types selected`.

The exact downstream record schema produced for each type is still being traced; the accepted request values themselves are now RECOVERED.

## RECOVERED startMapScan request

The host constructs the `startMapScan` bridge request with these exact fields:

- `scanRunId`
- `serverId`
- `worldId`
- `scanMode`
- `concurrency`
- `selectedTypes`
- `tileWidth`
- `tileHeight`

The request is assembled in the Map Scan state machine around RVA `0xFA983-0xFAD33`. The method name `startMapScan` is installed at RVA `0xFADC0`, with a recovered bridge-call timeout of `5,000 ms` at RVA `0xFADD9`.

LWBridge does not continue merely because the call returns. It reads the response field `accepted` and rejects a false/missing acceptance with `map scan was not accepted`. The rebuild should preserve this acknowledgement gate.

## RECOVERED native capture envelope and field vocabulary

Both verified xLua proxies contain the same native world-capture implementation markers. The native layer resolves `GameAssembly.dll` / IL2CPP metadata and hooks the recovered world manager mutation methods before feeding Map Scan capture queues.

Recovered IL2CPP/runtime class markers include `Assembly-CSharp`, `WorldPointManager`, `WorldTileInfo`, `PointInfo`, `ResPointInfo`, `WorldMarchDataManager`, `WorldMarch`, `WorldTroopManager`, `SFSObject`, and `SFSArray`. The proxy also resolves world-specific getters such as `GetResType`, `GetResLevel`, `GetWorldTreasureType`, `GetMarchCurPosIndex`, `GetMaxHP`, and `IsMonsterOrOrdinaryBoss`.

The native capture result/envelope exposes these exact field names:

`scanRunId`, `ready`, `points`, `marches`, `pointRemovals`, `marchRemovals`, `acks`, `error`, `dropped`, `pendingAcks`, `pendingMarchRemovals`, `pendingPointRemovals`, `pendingMarches`, `pendingPoints`.

Recovered record-field vocabulary includes:

`ownerUid`, `uuid`, `serverId`, `srcServerId`, `worldId`, `itemId`, `id`, `level`, `quality`, `state`, `curHp`, `curMaxHp`, `allianceId`, `playerName`, `alAbbr`, `name`, `power`, `specialType`, `protectEndTime`, `shieldEndTime`, `completionTime`, `cfgId`, `expiredTime`, `taskExpireTime`, `actEndTime`, `gatherMarchUuid`, `gatherUid`, `gatherAllianceId`, `lastHpTime`, `unavailableTime`, `recoverSpeed`, `fireSpeed`, `heroList`, `memberList`, `ownerServer`, `eventId`, `allianceAbbr`, `rewardUserList`, `diggingUserList`, `complete`, `ownerName`, `startTime`, `expireTime`, `createTime`, `fromPoint`, `multiple`, `killerId`, `_uuid`, `targetPos`, `startPos`, `homePos`, `ownerCurServerId`, `allianceUid`, `allianceName`, `endTime`, `status`, `_curHp`, `monsterId`, `monsterType`, `monsterSpecialType`, `monsterRallyNum`, `train`, `config`, `carriageNum`, `pointIds`, `isMainPoint`, `isSpecial`, `resType`, `resLevel`, `stolenCount`, `heroCount`, `memberCount`, `treasureType`, `rewardedCount`, `diggingCount`, `runtimeClass`, `configId`, `marchType`, `maxHp`, `isMonster`, `requiresRally`, `normalType`.

This proves the field vocabulary used by the native capture serializer. Exact per-record-kind grouping, optionality, numeric/string types, and map-index normalization remain UNKNOWN until the serializer branches are traced.

The proxy contains explicit states for `native world capture is initializing`, `native world capture hooks ready`, required-hooks-ready with optional hooks still pending, hooks unavailable, and `native world capture queue overflow run=`. Queue overflow therefore has an explicit detectable native failure condition and must not be silently ignored by the rebuild.

## RECOVERED native capture

The proxy exposes:

- `__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN`
- `__XluaBridgeNativeWorldCapture`
- `XluaBridgeMapScanTick`
- `XluaBridgeNativeUpdate`
- `XluaBridgePoll`

Recovered manager/method markers cover:

- `WorldPointManager.AddPointInfo` / remove/fold-up paths
- `WorldTileInfo.RemovePointInfo`
- `WorldMarchDataManager.AddMarch`, `UpdateMarch`, `AddOrUpdateMarch`, `TryRemoveMarch`
- `WorldTroopManager.UpdateTroop`

LWBridge also tracks `pendingPoints`, `pendingMarches`, point/march removals, pending acknowledgements, and a `dropped` native-capture condition.

## Evidence boundary

LWBridge's own direct-block/native-capture behavior must be recovered from the verified LWBridge artifact and validated independently against the current installed client. Do not fill unresolved semantics from unrelated implementations.

## Remaining unknowns

- Exact game-side block scheduling/tick implementation behind `XluaBridgeMapScanTick` after the recovered `startMapScan` request boundary.
- Exact block geometry/order and any mode-specific timing/pacing beyond the recovered `normal=8` / `fast=20` concurrency.
- Exact per-kind point/march field grouping, value types, optionality and map-index normalization.
- Retry/unread/failure transition rules.
- Resume cursor/persistence format.
- Native queue capacities and how `dropped` forces scan failure or retry.
- Exact map-index transaction/commit contract and final completion gate.

## Reliability design to implement

Persist progress before acknowledgement. Treat inflight blocks as uncertain after a disconnect and replay them idempotently. Freeze scheduling on bridge loss, save state, perform a clean injection bootstrap, then resume only unresolved work. Completion must require zero unresolved failed/unread blocks, no pending acknowledgements, no dropped native capture, and a stable final commit.
