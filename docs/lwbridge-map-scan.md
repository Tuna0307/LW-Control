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

The UI expects `map_search` to return `{rows,total}` and `map_data_options({serverId})` to return at least `serverId`, `alliances`, `names`, `dispatchLevels`, `counts`, `rewardItems`, `treasureTypes`, `noAllianceCount`, and `scanProgress`. `LWB-R6-003` below now implements the recovered default persisted-search slice offline: server/kind scope, `page`/`pageSize` pagination, `updatedAt asc|desc` plus `record_key ASC` stable ordering, `{rows,total}`, and city mark projection/`markedOnly`. `LWB-R6-005/006/007` extend this with recovered literal-substring keyword, city alliance/no-alliance, resource/monster name-key, truck/railway retained-item and reindeer-only, plus dispatch/ghost special-only predicates. `LWB-R6-008` adds an explicitly labelled **IMPLEMENTATION POLICY** that keeps the count and page reads inside one SQLite read snapshot, so `{rows,total}` cannot describe two different database generations when another connection publishes during a search. Other value-bearing filters and every non-`updatedAt` sort remain `UNKNOWN/BLOCKED`; the rebuild rejects those requests with the **IMPLEMENTATION POLICY** error `MAP_QUERY_UNRECOVERED` instead of approximating them. Omitted/null/empty-string filter fields are treated as absent, while explicit booleans such as treasure flags remain gated even when `false`. `map_data_options` and `map_city_export` remain blocked.

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

### LWB-R6-003 — frontend query contract and default persisted search (2026-09-08)

**RECOVERED source identity.** The immutable recovered frontend assets are `evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js` SHA-256 `fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e` and `evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js` SHA-256 `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`. The deterministic inspector `tools/inspect_lwbridge_map_query_frontend.py` verifies those hashes, locates the API wrappers and query-builder character range, checks all recovered query field names and sort keys, and pins the `Oe=50` default result page size plus `$n(1,200)` city-export query page size. Reproduce with `python tools\inspect_lwbridge_map_query_frontend.py evidence\lwbridge-0.3.1\frontend --json`; durable output is `evidence/lwbridge-implementation/2026-09-08-r6-map-query-frontend.json`.

**RECOVERED result.** The API wrappers are `map_search({kind,query})`, `map_data_options({serverId})`, and `map_city_export({query,...options})`. The verified query builder emits the field vocabulary listed above. Visible sort keys are `updatedAt`, `level`, `health`, `shield`, `distance`, `quality`, `power`, `remainingLootCount`, `arriveTime`, `protectTime`, `completionTime`, and conditional `itemCount`. Search consumes `rows` and `total`; city export requests the same city query builder with page `1` and page size `200`.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataStore.SearchIndexed` now serves the supported persisted subset instead of treating every valid search as unavailable. It uses the previously recovered `(kind,server_id,record_key)` map identity, recovered `LIMIT/OFFSET` pagination, `updated_at` ordering with recovered `record_key ASC` tie-breaking, and the recovered city `player_marks` join. City rows receive the visible `marked` boolean and `markedOnly=true` filters through that join. Deterministic tests cover multi-page results, equal-timestamp tie-breaking, large string owner UIDs, marks, and backend `{rows,total}` serialization.

**Validation and limits.** This is static frontend recovery plus offline SQLite/backend verification; it is not **LIVE-PROVEN** and does not prove scan ingestion correctness. The exact SQL predicate construction for nonempty keyword/alliance/resource/monster/treasure/quality/special/reindeer/item/completion/plunder/viewer/level filters and exact SQL expressions for non-`updatedAt` sorts are still **UNKNOWN/BLOCKED**. `MAP_QUERY_UNRECOVERED` and `MAP_INDEX_CORRUPT` are **IMPLEMENTATION POLICY** rebuild errors, not recovered LWBridge error codes. `map_data_options`, native Excel file creation, and per-kind native `record_key` derivation remain open.

### LWB-R6-004 — frontend option/summary consumers and stale-result guards (2026-09-08)

**RECOVERED source identity.** `tools/inspect_lwbridge_map_frontend_consumers.py` verifies the immutable `MapDataPanel-C1HVeNHr.js` and `api-ClPPi2JT.js` hashes above plus `index-sfL2sT3K.js` SHA-256 `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`. Durable output is `evidence/lwbridge-implementation/2026-09-08-r6-map-frontend-consumers.json`. Exact character locators include the `map_data_options` wrapper at API offset `21176`, `map_summary` wrapper at `21233`, options consumer at MapDataPanel offset `34234`, summary consumer at index offset `315014`, and map-search generation guard at MapDataPanel offset `37718`.

**RECOVERED result.** `map_data_options({serverId})` is consumed with top-level fields `serverId`, `alliances`, `names`, `dispatchLevels`, `counts`, `rewardItems`, `treasureTypes`, `noAllianceCount`, and `scanProgress`. The UI further consumes alliance `{name,count}` entries, resource/monster name `{key,count}` entries, numeric dispatch levels, truck/railway reward-item `{key,name,iconPath}` entries, and treasure-type entries containing `key`, `count`, `treasureType`, `suppliesType`, and `treasureNameKey`. The visible scan-progress consumer references `serverId`, `id`, `status`, `createdAt`, `updatedAt`, and `error` when present.

`map_summary` is consumed as `{serverId,counts,scanState}`. The parent stores the returned summary, routes `scanState` through the shared Map Scan state path, and only replaces summary counts when the server identity matches. This pins the response envelope but does not prove how the native backend selects the active summary server.

The per-tab query builder now has explicit conditional evidence. City only emits `withoutAlliance` and `markedOnly` when true; truck/railway/dispatch only emit `plunderableOnly` as true when enabled; dispatch omits `minLevel`/`maxLevel` when its level selector is empty. Treasure is materially different: `includeForeignRadarTreasures` and `luckyFirst` are emitted as booleans on treasure queries even when false. Therefore explicit false cannot be collapsed into an omitted filter while the corresponding SQL semantics remain unrecovered.

The frontend also has three independent stale-result guards. `map_search` increments `E.current` for each request and applies rows/total/error/loading effects only while its captured generation still matches. `map_data_options` uses `Ie.current` to ignore stale option responses. `map_summary` uses `Xe.current` and additionally refuses replacement after the selected profile changes.

**IMPLEMENTED/OFFLINE-TESTED.** `tests/LWBridge.Desktop.Checks/Program.cs` now feeds representative serialized query envelopes for all eight result tabs through `MapDataQueryContract.NormalizeSearch`. The matrix pins the current fail-closed unsupported-field set and separately verifies that numeric zero remains distinct from omission. Together with the existing explicit-false treasure check and empty-string/default checks, this protects the recovered false/zero/empty/omitted distinctions without pretending the missing SQL is known. The deterministic suite passes with `mapContract=true`.

**Validation and limits.** Reproduce with `python tools\inspect_lwbridge_map_frontend_consumers.py evidence\lwbridge-0.3.1\frontend --json` and `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj`. This is **RECOVERED static** plus **IMPLEMENTED/OFFLINE-TESTED** regression coverage, not **LIVE-PROVEN**. Complete `map_data_options` aggregation SQL/order/deduplication, `map_summary` backend server selection, remaining filter/sort SQL, and native city-export writing remain **UNKNOWN/BLOCKED**. Production `map_data_options` therefore remains fail-closed rather than synthesizing plausible option lists from stored rows.

### LWB-R6-005 — backend filter predicates, first verified subset (2026-09-08)

**RECOVERED source identity.** The verified reference is `..\LW\lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. `tools/inspect_lwbridge_map_query_backend.py` verifies that hash and locates the following raw PE file offsets: alliance `0x00C89B0C`, no-alliance `0x00C89B1D`, resource name key `0x00C89D59`, monster name key `0x00C89C68`, item-membership prefix `0x00C89EAF`, and item-key comparison `0x00C89F04`. Durable output is `evidence/lwbridge-implementation/2026-09-08-r6-map-query-backend.json`.

**RECOVERED result.** The original backend contains exact predicates `alliance_name = ?`, `(alliance_name IS NULL OR alliance_name = '')`, `CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?`, and `CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?`. The retained-item filter is an `EXISTS` over `json_each(...,'$.currentGoods')` whose item comparison is `CAST(json_extract(good.value,'$.key') AS TEXT) = ?`. Combined with the already recovered frontend emission rules, this supports city alliance/no-alliance, resource/monster name selection, and truck/railway retained-item filtering without guessing field meanings.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataQueryContract` now accepts only the recovered frontend-emitted forms for these filters, preserving mismatched kinds and unrecovered forms as `MAP_QUERY_UNRECOVERED`. `MapDataStore.SearchIndexed` applies the recovered predicates to both count and page SQL. Deterministic tests cover alliance, no-alliance, resource/monster name-key equality and truck `currentGoods` membership while retaining the existing fail-closed cases.

**Validation and limits.** Reproduce static evidence with `python tools\inspect_lwbridge_map_query_backend.py ..\LW\lwbridge-0.3.1.exe --json` and the implementation with `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -c Release`. This is **RECOVERED static** plus **IMPLEMENTED/OFFLINE-TESTED**, not **LIVE-PROVEN**. Keyword escaping and LIKE parameter construction, quality/special/reindeer handling, completion-status time source/units, per-kind plunderability, treasure visibility/lucky ordering, alternate sort mapping, `map_data_options`, `map_summary`, and export remained **UNKNOWN/BLOCKED** at this finding; `LWB-R6-006` below closes only the parameter-free special/reindeer boolean predicates.

### LWB-R6-006 — special/reindeer boolean predicates (2026-09-08)

**RECOVERED source identity.** The verified reference is `..\LW\lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. `tools/inspect_lwbridge_map_query_boolean_filters.py` verifies that hash, requires each field and predicate marker to occur exactly once, and requires each field name to be immediately followed by its SQL predicate. Raw PE file offsets are `specialOnly` `0x00C89F38`, its predicate `0x00C89F43`, `reindeerOnly` `0x00C8A0DD`, and its predicate `0x00C8A0E9`. Durable output is `evidence/lwbridge-implementation/2026-09-08-r6-map-query-boolean-filters.json`.

**RECOVERED result.** The exact predicates are `CAST(json_extract(data_json,'$.isSpecial') AS INTEGER) = 1` and `CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER) = 1`. At this checkpoint the frontend-kind correlation admitted `specialOnly=true` for dispatch/ghost and `reindeerOnly=true` for truck/railway. `LWB-R6-010` below supersedes only the railway kind allowance after a direct audit of the visible selector proved the `reindeer` option is truck-only. Explicit false and other mismatched-kind uses are not promoted into supported semantics.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataQueryContract` accepts only those frontend-emitted true/kind combinations. `MapDataStore.SearchIndexed` applies the exact JSON predicates to both count and page queries. Deterministic synthetic-row tests prove inclusion/exclusion and preserve mismatched kinds as `MAP_QUERY_UNRECOVERED`.

**Reproduction, validation and limits.** Run `python tools\inspect_lwbridge_map_query_boolean_filters.py ..\LW\lwbridge-0.3.1.exe --json` and `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj --configuration Release`. This is **RECOVERED static** plus **IMPLEMENTED/OFFLINE-TESTED**, not native-ingestion or live-game proof. Ordinary quality matching, keyword transformation, completion clocks, plunderability, treasure rules and alternate sorts remained **UNKNOWN/BLOCKED** at this finding; `LWB-R6-007` below closes keyword construction only.

### LWB-R6-007 — keyword escaping and parameter construction (2026-09-08)

**RECOVERED source identity and tools.** The verified reference remains `..\LW\lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. `tools/inspect_lwbridge_map_keyword.py` uses `pefile 2024.8.26` and `Capstone 5.0.6`, installed from their pinned PyPI packages in the recorded user-site locations. Reproduce setup with `python -m pip install --user capstone==5.0.6 pefile==2024.8.26`, then run `python tools\inspect_lwbridge_map_keyword.py ..\LW\lwbridge-0.3.1.exe --json`. Durable output is `evidence/lwbridge-implementation/2026-09-08-r6-map-keyword.json`.

**Exact locators and result.** The map-search function spans preferred-image VA `0x140271864`–`0x140276D7B`. At raw PE file offset `0x00C89A56` / xref VA `0x140272AD8`, the original uses the 176-byte predicate `(name LIKE ? ESCAPE '\' COLLATE NOCASE OR alliance_name LIKE ? ESCAPE '\' COLLATE NOCASE OR uuid LIKE ? ESCAPE '\' COLLATE NOCASE OR data_json LIKE ? ESCAPE '\' COLLATE NOCASE)`. The caller then invokes the same replacement routine three times in this exact order: backslash to `\\` (`0x00C89B06`, xref `0x140272B30`, character immediate `0x140272B4A`), percent to `\%` (`0x00C89B08`, xref `0x140272B5D`, immediate `0x140272B74`), and underscore to `\_` (`0x00C89B0A`, xref `0x140272B87`, immediate `0x140272B9E`). The bounded replacement helper at `0x1402948C1` copies each two-byte replacement.

The format descriptor at file offset `0x00C83ECF`, referenced at VA `0x140272BDD`, places one literal percent on each side of the fully escaped value. The loop at `0x140272BF9`–`0x140272C60` initializes a count of four and clones the resulting string once for each SQL placeholder. Therefore a nonempty keyword is a case-insensitive **literal substring**, not a caller-controlled wildcard, across `name`, `alliance_name`, `uuid` and raw `data_json`.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataQueryContract` now admits nonempty keyword strings for all recovered map kinds. `MapDataStore.SearchIndexed` performs the recovered replacements and percent wrapping, binds four identical values, and applies the exact four-column `NOCASE` predicate to count and page queries. Tests exercise each source column, case-insensitive matching, and a `%_\` input against a wildcard-shaped control row to prove that all three metacharacters remain literal.

**Validation and limits.** This is bounded **RECOVERED static** analysis plus **IMPLEMENTED/OFFLINE-TESTED** SQLite behavior. It is not live ingestion or scan proof. Ordinary quality, completion clocks, per-kind plunderability, treasure visibility/lucky order, options/summary aggregation and alternate sorts remain **UNKNOWN/BLOCKED**.

### LWB-R6-008 — consistent count/page SQLite snapshot (2026-09-08)

**IMPLEMENTATION POLICY source and rationale.** Project-manager review 4 identified that `MapDataStore.SearchIndexed` issued its recovered count and page SQL as separate reads protected only by the store instance's process-local lock. A second SQLite connection could therefore publish rows between the count and page queries. The original LWBridge transaction strategy for `map_search` has not been recovered, so this checkpoint does **not** claim an original transaction mode. The rebuild policy is that one `map_search` response must describe one database generation. The implementation uses Microsoft.Data.Sqlite `10.0.11` `BeginTransaction(deferred: true)`; the inspected package assembly SHA-256 is `4abd9c2a61e580eb853e93ca8953a3cef2c05714ae28d2d1859d4dbc5e5700bc`. The verified LWBridge reference remains SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`, but no new original behavior is inferred from it for this policy.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataStore.SearchIndexedCore` now starts one deferred SQLite transaction before the count query and assigns the same transaction to both count and page commands. A deterministic file-backed regression opens a second `MapDataStore` connection in WAL mode, commits a third city row after the first connection has observed a count of two, and then verifies that the in-flight search still returns `total=2` with two pre-existing rows. After the read transaction closes, the same database reports all three rows, proving that the writer committed without changing the search snapshot.

**Reproduction and limits.** Run `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj --configuration Release`; the deterministic result reports `mapContract=true`. Durable checkpoint metadata is `evidence/lwbridge-implementation/2026-09-08-r6-map-search-snapshot.json`. This is **IMPLEMENTATION POLICY** plus **IMPLEMENTED/OFFLINE-TESTED** behavior, not **RECOVERED** original transaction semantics and not **LIVE-PROVEN** scan ingestion. Advanced filters/sorts, `map_data_options`, authoritative summary state, export and per-kind native keys remain separately gated.

### LWB-R6-009 — option SQL family, treasure selector and dispatch-level filters (2026-09-08)

**RECOVERED source identity and locators.** The verified reference remains `..\LW\lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`; the correlated recovered frontend `MapDataPanel-C1HVeNHr.js` remains SHA-256 `fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e`. Bounded raw-PE printable-string inspection records the option SQL family at `0x00C83048-0x00C83A72`, the latest server scan-summary query at `0x00C849FF-0x00C84AC5`, level predicates around `0x00C89BE4-0x00C89C1A`, treasure predicates at `0x00C89D3E-0x00C89F37`, and adjacent derived-sort strings at `0x00C8A918-0x00C8ABEB`. The durable focused excerpt and metadata are `evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.txt` and `.json`.

**RECOVERED result.** The option producer exposes city alliance counts ordered `COLLATE NOCASE`, resource/monster name-key counts grouped by kind/name, distinct dispatch levels `>=1` ordered ascending, and treasure option grouping that deliberately normalizes exactly one dimension: `suppliesType>0` produces `supplies_type` with `treasure_type=0`, while ordinary treasure rows produce `treasure_type` with `supplies_type=0`. The reward-item option query groups truck/railway `currentGoods` `{key,name,iconPath}` and excludes expired arrivals through `arriveTs > ?`, but the producer/unit for that cutoff parameter remains unrecovered. `scanProgress` reads the newest non-`discarded` `scan_runs` row for the selected server using `ORDER BY updated_at DESC LIMIT 1`.

The persisted-search builder separately exposes exact treasure predicates `CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) = ?`, `CAST(json_extract(data_json,'$.treasureType') AS INTEGER) = ?`, and the ordinary-treasure guard `COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) = 0`. Dispatch level filtering uses `level >= ?` and `level <= ?`; the recovered frontend emits the same selected dispatch level into `minLevel` and `maxLevel`.

**IMPLEMENTED/OFFLINE-TESTED.** `MapDataQueryContract` now accepts only the recovered treasure-selection shape: both numeric dimensions are present, exactly one is positive and the other is zero. `MapDataStore.SearchIndexed` applies the corresponding supplies predicate or ordinary treasure predicate plus the recovered zero guard. Dispatch positive `minLevel`/`maxLevel` values are also admitted for the recovered dispatch kind and applied to both count and page SQL. Deterministic rows prove ordinary treasure versus supplies isolation, reject partial/both-positive treasure selections, and prove an exact dispatch-level result.

**Validation and limits.** `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj --configuration Release` reports `mapContract=true`. A convenience executable verifier attempted after the successful bounded byte inspection was rejected by the active environment's automatic safety review; it was not retried or routed through another executor. The saved excerpt records only observations already obtained before that rejection. `map_data_options` therefore remains fail-closed until the reward-item `arriveTs` cutoff clock/source/unit is recovered. Ordinary quality binding, completion-status clocks, per-kind plunderability parameters, foreign-radar/lucky treasure behavior, authoritative `map_summary` server selection and remaining non-`updatedAt` sort mapping remain **UNKNOWN/BLOCKED**. This finding is static plus offline SQLite proof, not native-ingestion or live-game proof.

### LWB-R6-010 — quality selector kind gates and railway reindeer correction (2026-09-08)

**RECOVERED source identity and locators.** The immutable recovered `MapDataPanel-C1HVeNHr.js` remains SHA-256 `fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e`. Character offset `5977` defines quality-capable kinds as `truck`, `railway`, `dispatch` and `ghost`; offset `9155` maps numeric display quality `1=N`, `2=R`, `3=SR`, `4=SSR`, and every value `>=5` to `UR`. The query builder at offsets `37243`/`37279` omits ordinary `quality` when the selected value is `special` or `reindeer`, emitting `specialOnly`/`reindeerOnly` instead. The visible selector around `52301` offers ordinary `n/r/sr/ssr/ur`, `special` only for dispatch/ghost, and at offset `52723` the `reindeer` option is explicitly guarded by `F===truck`. Durable evidence is `evidence/lwbridge-implementation/2026-09-08-r6-map-quality-selector-kind-gates.txt` and `.json`.

**IMPLEMENTED/OFFLINE-TESTED correction.** `MapDataQueryContract` now accepts `reindeerOnly=true` only for truck. Railway `reindeerOnly=true` returns to `MAP_QUERY_UNRECOVERED`, and a deterministic regression pins that fail-closed behavior. The already recovered binary predicate `CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER) = 1` remains unchanged.

**Validation and limits.** This corrects an overbroad frontend-kind interpretation from `LWB-R6-006`; it does not alter the recovered predicate itself. Ordinary quality filtering remains **UNKNOWN/BLOCKED** because the frontend display mapping proves `UR` covers all numeric qualities `>=5`, while the exact original backend filter/range binding is still unrecovered. The rebuild therefore must not guess `ur` as either equality to `5` or `>=5`. This is recovered frontend evidence plus offline contract verification, not a live-game query proof.

### LWB-R6-011 — schema metadata, future-schema guard and legacy-import markers (2026-09-08)

**RECOVERED source identity and locators.** The verified `..\LW\lwbridge-0.3.1.exe` remains SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. Bounded raw-PE printable-string inspection recovers `SELECT value FROM metadata WHERE key = 'schema_version'` at `0x00C82CED`, the schema-version metadata upsert beginning at `0x00C82E28`, future-schema message fragments at `0x00C82EDE`/`0x00C82EF4`, and `MAP_SCHEMA_TOO_NEW` at `0x00C82F0F`. The adjacent migration SQL at `0x00C82D50` cancels dispatch-assist rows in `scheduled`, `waiting_connection`, `running` or `retry_wait` with `last_error='legacy assist schedule replaced'`. `legacy_import_completed` appears at `0x00C84846`, and the metadata write with literal value `true` begins at `0x00C848A0`. Durable evidence is `evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.txt` and `.json`.

**RECOVERED result.** The original database has an explicit schema-version read/write path and a distinct future-schema failure branch. It also records completion of the legacy import through metadata and contains a specific dispatch-assist migration update. These are recovered original contracts; they do not by themselves reveal which numeric schema version is supported, which prior version triggers the migration, or the unit/source of the bound `updated_at` values.

**UNKNOWN/BLOCKED implementation boundary.** `MapDataStore` is intentionally unchanged by this finding. Writing a guessed schema number, choosing a guessed migration threshold, or inventing the metadata timestamp clock/unit would violate the recovery rule. A direct read-only query of the existing user-profile `map-data.db` was rejected by the current execution environment's automatic safety review and was not retried through another executor; no value from that database is promoted into this finding. Recover the version constant/migration selection and timestamp producer from permitted evidence before implementing the future-schema gate and versioned migration tests.

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
