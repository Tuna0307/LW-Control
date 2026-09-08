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

## RECOVERED Map Data query/presentation contract

The verified 0.3.1 frontend now also establishes the offline query shape used after records reach the map index. `map_search` is called as `{kind, query}`. The result table uses a fixed recovered page size of **50** and starts each kind with `updatedAt desc`. The query builder can emit these fields:

`serverId`, `keyword`, `resourceNameKey`, `monsterNameKey`, `treasureType`, `suppliesType`, `alliance`, `withoutAlliance`, `markedOnly`, `page`, `pageSize`, `sorts`, `quality`, `specialOnly`, `reindeerOnly`, `itemKey`, `completionStatus`, `plunderableOnly`, `includeForeignRadarTreasures`, `luckyFirst`, `viewerUid`, `viewerAllianceId`, `minLevel`, `maxLevel`.

The UI expects `map_search` to return `{rows,total}` and `map_data_options({serverId})` to return at least `serverId`, `alliances`, `names`, `dispatchLevels`, `counts`, `rewardItems`, `treasureTypes`, `noAllianceCount`, and `scanProgress`. The rebuild now validates the recovered search envelope/kind/server/page/sort boundary offline and returns `MAP_INDEX_UNAVAILABLE` after validation until an authoritative persistent index exists.

Recovered visible row vocabulary is narrower than the native capture vocabulary and gives useful normalization targets without yet proving backend identity rules:

- city: `ownerUid`, `ownerName`, `allianceName`, `level`, `health`, `protectEndTime`/`shieldEndTime`, `marked`, coordinates, `updatedAt`;
- resource: `resourceNameKey`, `level`, gathering state, coordinates, `updatedAt`;
- monster: `monsterNameKey`, `level`, `distanceFromHome`, coordinates, `updatedAt`;
- truck: `uuid`, `ownerName`/`allianceName`, `quality`, `isSpecialURQuality`, `power`, `currentGoods`, `robTimes`, `maxLootCount`, `protectTime`, `arriveTs`, `updatedAt`;
- railway: `allianceName`/`allianceAbbr`, `quality`, `power`, `currentGoods`, `protectTime`, coordinates, `updatedAt`;
- dispatch/ghost: `ownerName`/`ownerUid`, `level`, `quality`, `isSpecial`, task status, `rewards`, `completionTime`, coordinates, `updatedAt`;
- treasure: `uuid`, `treasureType`, `suppliesType`, `treasureNameKey`, `remainingBoxes`, claim/world-state fields, coordinates, `updatedAt`.

For rendering only, the frontend derives a row key from `uuid`, else `marchUuid`, else `recordKey`, else a composite of `pointIndex`, `ownerUid` and `updatedAt`, then prefixes kind and server. This **does not prove the authoritative persistent identity/update/removal key**. R6 storage must remain blocked on that distinction rather than promoting a React rendering fallback into a database key.

### RECOVERED persistent map-index schema and identities

Static strings from the verified `lwbridge-0.3.1.exe` now expose the original SQLite map-index schema directly. The database filename is `map-data.db`; connection setup includes `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON` and `busy_timeout=5000`. The original tables are `metadata`, `map_records`, `scan_runs`, `scan_blocks`, `scan_records`, `player_marks`, `app_settings`, `treasure_claim_states`, `dispatch_plunder_jobs`, `truck_plunder_jobs`, `truck_plunder_history` and `dispatch_assist_jobs`, with the recovered indexes reproduced in `MapDataStore.cs`.

The authoritative **stored** record identity is now proven as `PRIMARY KEY (kind, server_id, record_key)`. Scan staging uses `PRIMARY KEY (run_id, kind, server_id, record_key)`. This resolves the database identity question but does **not** yet recover how each native city/resource/monster/truck/railway/dispatch/ghost/treasure payload is converted into its `record_key`; production scan ingestion therefore remains fail-closed on that derivation.

Player marks use `PRIMARY KEY (server_id, owner_uid)`. The original lookup/delete/upsert SQL all use those two fields, and map search joins city rows to marks using the same `server_id` plus `CAST(json_extract(data_json,'$.ownerUid') AS TEXT)`. The frontend only calls `map_player_mark_set` when a row has `ownerUid`, and listens to `bridge://player-mark-changed` only as a refresh trigger. The rebuild now reproduces this key, mark/unmark persistence and refresh event offline.

The original server-clear transaction contains `DELETE FROM scan_runs WHERE server_id=?1` followed by `DELETE FROM map_records WHERE server_id=?1`. `player_marks` is not deleted, so marks survive a Map Data clear/rescan. The rebuild reproduces and tests that scope. Direct-scan completion separately replaces one kind/server from staging: it deletes that kind/server from `map_records`, then copies the matching `scan_records` for the completed run. Strings `INCOMPLETE_SCAN`, `direct map scan contains failed batches` and `direct map scan is incomplete` show publication is gated on scan completeness.

The original map-search SQL also exposes several query rules: keyword search checks `name`, `alliance_name`, `uuid` and `data_json` with case-insensitive `LIKE`; alliance and no-alliance use the indexed `alliance_name`; city marked state is joined from `player_marks`; pagination uses `LIMIT/OFFSET`; and stable ordering includes `record_key ASC` as a tie-breaker. Additional recovered sort expressions include distance, power, completion time, remaining loot, protection/arrival time, special-quality normalization, health and current-goods item counts. These fragments are evidence for the next R6 query implementation; filters whose exact SQL is not yet recovered remain gated instead of being approximated.

## Remaining unknowns

- Exact game-side block scheduling/tick implementation behind `XluaBridgeMapScanTick` after the recovered `startMapScan` request boundary.
- Exact block geometry/order and any mode-specific timing/pacing beyond the recovered `normal=8` / `fast=20` concurrency.
- Exact per-kind point/march field grouping, value types, optionality and map-index normalization.
- Exact per-kind derivation of the authoritative `record_key` before map/scan upsert.
- Retry/unread/failure transition rules.
- Resume cursor/persistence format.
- Native queue capacities and how `dropped` forces scan failure or retry.
- Exact map-index transaction/commit contract and final completion gate.

## Reliability design to implement

Persist progress before acknowledgement. Treat inflight blocks as uncertain after a disconnect and replay them idempotently. Freeze scheduling on bridge loss, save state, perform a clean injection bootstrap, then resume only unresolved work. Completion must require zero unresolved failed/unread blocks, no pending acknowledgements, no dropped native capture, and a stable final commit.
