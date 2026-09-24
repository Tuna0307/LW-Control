# LWBridge 0.3.1 — exact Map Data control-plane contracts

**Date:** 2026-09-24
**Role:** secondary researcher, read-only strict-parity lane
**Repository inspected:** `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
**Reference:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**Reference SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Purpose and boundary

This handoff recovers the original LWBridge 0.3.1 **Map Data control plane** far enough to implement the host/UI contracts that are currently missing or deviating. It deliberately stops where control enters still-protected game-side scripts or unrecovered serializer branches. Current Last War v19/v20/v21 behavior and the existing rebuild are used only as compatibility/reconstruction evidence, never as product authority.

Evidence labels used below:
- **EXACT_BYTES** — immutable recovered original frontend bytes.
- **EXACT_CONTRACT** — original native/frontend behavior recovered with durable reference locators.
- **PARTIAL** — public boundary is known but a nested serializer/branch is still unresolved.
- **PROTECTED/UNKNOWN** — execution crosses into unrecovered `bridge-scripts.dat` / game-side handler logic; do not invent.

Primary immutable frontend sources:
- `api-ClPPi2JT.js`, SHA-256 `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`.
- `MapDataPanel-C1HVeNHr.js`, SHA-256 `fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e`.
- `index-sfL2sT3K.js`, SHA-256 `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`.

## Cross-cutting frontend host wrapper

**EXACT_BYTES.** API helper `U(command,payload)` automatically injects the currently selected `profileId` into object payloads that do not already contain `profileId`. Therefore the request shapes below list feature-owned fields; normal original calls can additionally carry the selected profile scope.

Original API wrapper character locators in `api-ClPPi2JT.js`:

| Command | Character offset | Exact wrapper shape |
|---|---:|---|
| `map_scan_status` | 19677 | `U('map_scan_status')` |
| `server_jump` | 19720 | `U('server_jump',{serverId:e})` |
| `map_scan_start` | 21040 | `U('map_scan_start',e)` |
| `map_scan_stop` | 21083 | `U('map_scan_stop')` |
| `map_scan_clear` | 21124 | `U('map_scan_clear',{serverId:e})` |
| `map_data_options` | 21179 | `U('map_data_options',{serverId:e})` |
| `map_summary` | 21236 | `U('map_summary',Z(profileId))` |
| `map_search` | 21347 | `U('map_search',{kind:e,query:t})` |
| `map_city_export` | 21404 | `U('map_city_export',{query:e,...options})` |
| `map_dispatch_share_alliance` | 21462 | rows are projected to `uuid,serverId,x,y,cfgId,ownerName,allianceAbbr` |
| `map_treasure_claim` | 21667 | `{serverId,claimScope,prioritizeLuckySlots,targetUuid}` |
| `map_treasure_state_refresh` | 21777 | `{serverId,records}` |
| `map_treasure_state_refresh_all` | 21854 | `{serverId}` |
| `map_treasure_claim_status` | 21924 | no feature payload |
| `map_plunder_jobs_list` | 21976 | no feature payload |
| `map_dispatch_plunder_schedule` | 22029 | `{rows:[...]}` with frontend-owned delay randomization |
| `map_dispatch_plunder_cancel` | 22386 | `{serverId,taskUuid}` |
| `map_truck_plunder_schedule` | 22482 | `{rows:[...executeAt...]}` |
| `map_truck_plunder_cancel` | 22581 | `{serverId,trainUuid}` |
| `map_player_mark_set` | 22660 | `{row,marked}` |

---

# 1. Manual Map Scan control plane

## 1.1 Exact scan kinds, defaults and mode UI

**EXACT_BYTES / EXACT_CONTRACT.** The original product has exactly eight Map Data scan kinds, in this order:

`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`.

There is no original `zombie_boss` scan kind.

`MapDataPanel` default scan state is:

```text
serverId=0
serverIdSource='none'
scanRunId=''
isReading=false
phase='idle'
selectedTypes=[all eight]
totalBlocks=0
readBlocks=0
unreadBlocks=0
failedBlocks=0
inflightBlocks=0
scanMode='normal'
concurrency=8
retryCount=2
scanRate=0
progressPercent=0
nativeCaptureReady=false
nativePendingRecords=0
nativeDroppedRecords=0
```

Manual mode preference key is exactly `lwbridge.mapScanMode`. Stored exact `normal` or `fast` is accepted; otherwise the current scan state's mode or `normal` is used. The original UI exposes Normal/Fast controls and persists the selection.

Native mode contract: only `normal` and `fast` are valid; `normal -> concurrency 8`, `fast -> concurrency 20`. Additional mode-specific pacing/retry behavior is **UNKNOWN**.

## 1.2 `map_scan_start`

### Public request

**EXACT_BYTES.** Manual Start calls:

`map_scan_start({selectedTypes, scanMode})`

Original Auto Scan calls:

`map_scan_start({selectedTypes, scanMode, resume:false})`

`resume` is an optional native input. `resume:true` only selects the existing-state route if current serialized state contains exact boolean `resumeAvailable=true`. Every recovered state construction/reset writes `resumeAvailable=false`; no true-producing constructor has been recovered.

### Selected-type normalization

**EXACT_CONTRACT**, filter RVA `0x20D26F-0x20D416`, allowlist table RVA `0xC81908`, fallback table RVA `0x825D98`:

- absent or non-array `selectedTypes` => all eight kinds;
- array entries must be exact strings from the eight-value allowlist;
- unknown/non-string entries are discarded;
- duplicate accepted entries are removed preserving first accepted occurrence;
- zero valid entries => `INVALID_SCAN_TYPES / no valid map scan types selected`.

### Host admission and world readiness

**EXACT_CONTRACT**, shared worker `0x1400F9333-0x1400FB8D8`:

1. Missing game connection => `GAME_CONNECTION_UNAVAILABLE / game connection unavailable`.
2. Existing active scan => `SCAN_RUNNING / map scan already running`.
3. Shared state producer reads `getWorldMapState`, then `getCurrentServerId`, each through the common 5,000 ms bridge request.
4. If not in world map, host sends `enterWorldMap` with a 5,000 ms request timeout.
5. Readiness deadline is 10,000 ms, polled every 500 ms.
6. Failure to enter ready world map => `WORLD_MAP_FAILED / failed to enter world map`.
7. After readiness, server identity must be positive and `serverIdSource` must be exact `live`; otherwise `SERVER_UNAVAILABLE / current server id unavailable`.
8. Positive tile dimensions are required; otherwise `MAP_SIZE_UNAVAILABLE / world map dimensions are unavailable`.
9. Logical block count is `ceil(tileWidth/20) * ceil(tileHeight/20)`.

### Protected `startMapScan` bridge envelope

**EXACT_CONTRACT**, request assembly around RVA `0xFA983-0xFAD33`, method install near RVA `0xFADC0`:

```text
scanRunId
serverId
worldId
scanMode
concurrency
selectedTypes
tileWidth
tileHeight
```

The internal `startMapScan` request timeout is 5,000 ms. The host requires response field `accepted`; false/missing acceptance rejects with exact message `map scan was not accepted` (the public error code paired with that message is not yet pinned).

### Public success result

**EXACT_BYTES consumer contract.** Manual and Auto callers treat the public `map_scan_start` result as the new Map Scan state and feed it into the shared scan-state setter. Do not return a separate `{accepted:true}` object at the public command boundary; `accepted` belongs to the nested game-side request.

### Protected boundary

**PROTECTED/UNKNOWN:** after the `startMapScan` request is accepted, exact game-side traversal/order, `XluaBridgeMapScanTick` scheduling, block-coordinate acquisition, retries, native acknowledgement/drain policy and protected extraction internals remain unrecovered. Preserve the recovered envelope/state/publication gates; do not design substitute observable behavior from the current rebuild.

## 1.3 `map_scan_status` and shared scan state

### Request/result

**EXACT_BYTES.** No feature payload. The result is the current shared Map Scan state.

The immutable frontend requires the core fields listed in the default object above. Native live-state metadata additionally proves fields including `createdAt`, `startedAt`, `resumeAvailable`, `nativePendingRecords`, `nativeDroppedRecords`, `nativeCaptureReady`, plus world/server context. The complete serialized field order/set beyond the frontend-consumed core is **PARTIAL** and should not be expanded from rebuild-only fields.

### Shared server/world refresh

**EXACT_CONTRACT**, producer `0x1400F7F47-0x1400F9333`:

- bridge request order is `getWorldMapState` then `getCurrentServerId`, 5,000 ms each;
- `isInWorld` missing/non-boolean normalizes false in the recovered world-state branch;
- positive `homeServerId` is retained; season/truck-match server arrays filter to `1..99999`, dedupe and sort;
- positive current server writes `serverIdSource='live'`;
- if `isReading===true`, existing positive state server differs from positive current server => exact message `current server changed during map scan` rather than silently switching;
- if current server is unavailable while actively reading, existing state is preserved;
- otherwise a remaining nonpositive state falls to `serverId=0`, `serverIdSource='none'`, `tileWidth=0`, `tileHeight=0`.

`getCurrentServerId.serverId <= 0` maps to `SERVER_UNAVAILABLE / current server id unavailable`; other transport failures propagate instead of being renamed.

### Progress/event normalization

**EXACT_CONTRACT.** Internal game events `map.scan.progress`, `map.scan.complete`, `map.scan.error` are admitted only while `isReading=true`. If an event supplies nonempty `scanRunId`, it must byte-match the active run; if it supplies positive `serverId`, it must match the active server. Missing/empty run ID and zero/missing server ID are tolerated.

Progress normalization:

- `completedBlocks = clamp(input,0,totalBlocks)`;
- `failedBlocks = clamp(input,0,totalBlocks)`;
- if completed + failed > total => `INVALID_SCAN_PROGRESS`;
- `inflightBlocks = clamp(input,0,min(concurrency,total-completed-failed))`;
- `readBlocks = completedBlocks`;
- `unreadBlocks = total-completed-failed-inflight`;
- `scanRate = round(completedBlocks / max(elapsedMs,1) * 100000) / 100`.

The frontend receives status updates through `bridge://map-scan-status`; the game-side `map.scan.*` events are an internal host/proxy boundary, not frontend event names.

## 1.4 Completion/publication gates

**EXACT_CONTRACT.** Publication is not equivalent to “loop ended”:

- positive `failedBlocks` contributes terminal failure;
- any present nonempty `lastError` contributes terminal failure (whitespace is not trimmed);
- a nonempty failure list routes through failure/Stop cleanup;
- only an empty failure list enters `phase='publishing'` and invokes direct completion;
- direct completion requires `completedBlocks + failedBlocks == totalBlocks`;
- nonzero failed blocks => `INCOMPLETE_SCAN / direct map scan contains failed batches`;
- completion transition SQL is `UPDATE scan_runs SET status='completed',error=NULL,updated_at=?1 WHERE id=?2 AND status='running'`;
- zero affected transition rows => `INVALID_SCAN / map scan is not running`;
- post-commit missing run => `INVALID_SCAN / map scan disappeared`.

Native pending count is exactly `pendingPoints + pendingMarches + pendingPointRemovals + pendingMarchRemovals + pendingAcks`. Dropped records are tracked separately and positive dropped values enter failure handling before publication.

## 1.5 `map_scan_stop`

**EXACT_BYTES consumer + EXACT_CONTRACT cleanup.** No feature payload. The original frontend awaits the returned state and installs it as current state.

Both recovered cleanup paths conditionally send protected `stopMapScan` before state publication. Stopped-state writes are:

`isReading=false`, `phase='idle'`, `inflightBlocks=0`, `resumeAvailable=false`.

Stop-specific uncommon error branches outside the active bridge/session predicate remain **PARTIAL**. Do not substitute current rebuild cancellation vocabulary as original without evidence.

## 1.6 `map_scan_clear`

**EXACT_CONTRACT**, original handler `0x14034475E-0x1403449E6`.

Request: `{serverId}`.

Admission:
- active scan => `SCAN_RUNNING / stop the map scan first`;
- otherwise requested `serverId` must be positive, must equal current scan-state `serverId`, and current `serverIdSource` must equal exact `live`;
- any failure of that server gate => `SERVER_UNAVAILABLE / current server id unavailable`.

Only after those gates does the original perform server-scoped clear. Original storage clear deletes `scan_runs` and `map_records` for that server; it does **not** delete `player_marks`. After clear, state is refreshed, `phase='idle'`, and updated state is returned/published.

### Required parity correction

Current rebuild behavior allowing `serverId=0` clear-all and clearing saved/non-live server data is a direct deviation. Remove that product behavior for strict 0.3.1 parity.

---

# 2. `server_jump`

## Public contract

**EXACT_BYTES / EXACT_CONTRACT.** Request: `{serverId}`.

Original frontend consumes success fields:

`changed`, `previousServerId`, `serverId`.

Auto Scan logs either `switched <previousServerId> -> <serverId>` when `changed` is true or `already on server <serverId>` when false. Therefore `serverId` is part of the observable success contract and must not be omitted.

Recovered public errors:

- invalid ID => `INVALID_SERVER_ID / server ID must be an integer from 1 to 99999`;
- conflicting operation => `GAME_OPERATION_IN_PROGRESS / another game operation is already in progress`;
- travel did not reach target => `SERVER_JUMP_TIMEOUT / the game did not switch to the target server`.

The exact original numeric server-jump timeout duration is **UNKNOWN**. Do not reuse the rebuild's 15 s/18 s values as original; those are current implementation policy.

## Protected boundary

The exact original game-side server travel implementation after host validation remains protected/current-client-sensitive. Implement the public validation/result/error contract and wait for authoritative arrival; do not claim the current Last War `GoToUtil/CrossServerUtil` route was the 0.3.1 protected implementation.

### Required parity correction

Current backend success omits `serverId`; restore it.

---

# 3. `map_summary`

## Public request/result

**EXACT_BYTES.** Wrapper accepts optional explicit profile scope; normal generic wrapper can inject selected profile.

**EXACT_CONTRACT** successful response contains exactly the original envelope:

`{serverId, counts, scanState}`.

`counts` uses exactly the eight original kind keys, with zero as the frontend default for absent populations.

## Native flow

**EXACT_CONTRACT**, native future `0x140151509-0x140152388`:

1. Await shared scan-state producer.
2. Extract `scanState.serverId`.
3. Compute optional active run scope via selector helper `0x140344F1D-0x140345030`.
4. Read counts for server/source scope.
5. Only after both state and counts succeed assemble `serverId`, `counts`, `scanState`.

Run-scope selector:

- staging only when requested/current server is positive, state `isReading=true`, state server matches, and `scanRunId` is a raw string with length > 0;
- staging source is `scan_records WHERE server_id=?1 AND run_id=?2`;
- otherwise source is `map_records WHERE server_id=?1`;
- whitespace-only run IDs count as nonempty; no trim is evidenced.

Shared-state or count errors propagate. Original does **not** replace failures with a synthetic server 0 / zero-count summary.

### Required parity correction

Do not expose rebuild-only `savedServerIds`, `saved_profile_index`, or saved-server fallback as original `map_summary` fields/selection policy. Those were later rebuild workflow decisions.

---

# 4. `map_data_options`

## Request/result envelope

**EXACT_BYTES.** Request `{serverId}`.

Frontend consumes:

`serverId, alliances, names, dispatchLevels, counts, rewardItems, treasureTypes, noAllianceCount, scanProgress`.

Nested shapes consumed:

- `alliances[]`: `{name,count}`;
- `names.resource[]`, `names.monster[]`: `{key,count}`;
- `dispatchLevels[]`: numeric values;
- `rewardItems.truck[]`, `rewardItems.railway[]`: `{key,name,iconPath}`;
- `treasureTypes[]`: `{key,count,treasureType,suppliesType,treasureNameKey}`;
- `scanProgress`: frontend reads `serverId,id,status,createdAt,updatedAt,error` when present.

Native response assembly order is recovered as:

`serverId, counts, alliances, names, dispatchLevels, noAllianceCount, rewardItems, treasureTypes, scanProgress`.

## Exact source selector

Same original selector as summary:

- active staging only for requested positive server + `isReading=true` + matching scan-state server + raw nonempty `scanRunId`;
- staging uses `scan_records` with server/run scope;
- otherwise published `map_records` with server scope.

## Option aggregation facts

**EXACT_CONTRACT:**

- City alliances are grouped/count ordered by `alliance_name COLLATE NOCASE, alliance_name`.
- NULL and empty City alliance groups are excluded from `alliances[]` and accumulated into `noAllianceCount`.
- Resource/Monster name options exclude empty keys and are grouped/count ordered NOCASE.
- Dispatch levels are distinct integers >=1 ordered ascending.
- Treasure options normalize Supplies rows to `treasureType=0` and ordinary Treasure rows to `suppliesType=0`, then group/count in recovered stable order.
- Truck/Railway reward options derive current-goods key/name/icon groups and use the same precise Unix-ms `arriveTs` cutoff clock recovered for Map search.
- Eight counts are computed from the same selected source/scope.

Persisted `scanProgress` source SQL is exact:

`SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs WHERE server_id=?1 AND status<>'discarded' ORDER BY updated_at DESC LIMIT 1`.

For an active scope, the exact run ID is selected instead. The complete nested `scanProgress` serializer key set/field order and absent-row representation remain **PARTIAL**; only the fields consumed by the frontend are safe implementation requirements today.

### Required parity corrections

- Do not add `zombie_boss` name/count families.
- Do not add rebuild-only `monsterLevels` to the original public envelope.
- Preserve staged-vs-published selector instead of always reading saved/published data.

---

# 5. `map_search`

## Request and paging

**EXACT_BYTES.** Request `{kind,query}`. Original public kinds are only the eight Map Data kinds.

Default page size is exactly `50`. Every kind starts with default sort `[{sortBy:'updatedAt',sortOrder:'desc'}]`. City Excel reuses the same query builder with page `1`, page size `200`.

Original query builder emits:

`serverId, keyword, resourceNameKey, monsterNameKey, treasureType, suppliesType, alliance, withoutAlliance, markedOnly, page, pageSize, sorts, quality, specialOnly, reindeerOnly, itemKey, completionStatus, plunderableOnly, includeForeignRadarTreasures, luckyFirst, viewerUid, viewerAllianceId, minLevel, maxLevel`.

Important kind ownership from the immutable original builder:

- `resourceNameKey`: Resource only;
- `monsterNameKey`: Monster only;
- alliance/no-alliance/marked: City only;
- treasure type/supplies + viewer/radar/lucky fields: Treasure only;
- item key: Truck/Railway only;
- completion status: Dispatch/Ghost;
- public `plunderableOnly`: Truck/Railway/Dispatch;
- `specialOnly`: Dispatch/Ghost;
- reindeer special selection is a Truck/Railway backend predicate family, with the original UI controlling where the selector is exposed;
- **minLevel/maxLevel are emitted by the original panel for Dispatch's selected level; the rebuild-added Resource/Monster level UI is not reference authority.**

Original success envelope is exactly consumed as `{rows,total}`. Search results are generation-guarded in the frontend: a stale response from an older query must not overwrite a newer search.

## Exact recovered SQL/filter rules

### Keyword

Case-insensitive literal substring over `name`, `alliance_name`, `uuid`, `data_json`:

`(name LIKE ? ESCAPE '\' COLLATE NOCASE OR alliance_name LIKE ? ESCAPE '\' COLLATE NOCASE OR uuid LIKE ? ESCAPE '\' COLLATE NOCASE OR data_json LIKE ? ESCAPE '\' COLLATE NOCASE)`

Escape order is backslash -> `\\`, percent -> `\%`, underscore -> `\_`; then wrap one literal `%` around the escaped keyword and bind four copies.

### City

- alliance: `alliance_name = ?`;
- no alliance: `(alliance_name IS NULL OR alliance_name = '')`;
- `markedOnly` uses the `player_marks` join on server + City `ownerUid`.

### Resource / Monster names

- Resource: `CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?`;
- Monster: `CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?`.

### Treasure selector

- Supplies: `CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) = ?`;
- ordinary Treasure: `CAST(json_extract(data_json,'$.treasureType') AS INTEGER) = ?` plus `COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) = 0`.

### Quality

Frontend selectors:

- `n` => `quality = 1`;
- `r` => `quality = 2`;
- `sr` => `quality = 3`;
- `ssr` => `quality = 4`;
- `ur` => `quality >= 5`.

Truck ordinary UR adds `COALESCE(CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER),0) = 0`.

`specialOnly`: `CAST(json_extract(data_json,'$.isSpecial') AS INTEGER) = 1`.

`reindeerOnly`: `CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER) = 1`.

### Goods item filter

`itemKey` matches a `currentGoods` element where `CAST(json_extract(good.value,'$.key') AS TEXT) = ?`.

### Time/completion/plunder rules

Original wall clock uses `GetSystemTimePreciseAsFileTime`, converted to Unix milliseconds.

- Truck/Railway always apply active-arrival: `(arriveTs IS NULL OR arriveTs > now)`.
- Truck/Railway plunderable adds arrival-present and remaining-loot > 0.
- Dispatch/Ghost native plunderability requires positive completion/plunder time, a nonexpired task and remaining steals; the original public frontend emits this flag for Dispatch, not Ghost.
- completion pending: completionTime is null, <=0, or > now.
- completion completed: completionTime >0 and <= now.

Dispatch level predicates are inclusive `level >= ?` and `level <= ?`; the original level selector emits the same selected value to both bounds.

## Treasure visibility/state cache

**EXACT_CONTRACT.** `includeForeignRadarTreasures` and `luckyFirst` are explicitly emitted even when false.

When foreign radar is false:

- non-radar rows (`treasureType != 1`) pass;
- radar rows require the viewer alliance;
- with explicit `viewerAllianceId`, compare against it;
- without explicit viewer alliance, fall back to the row's `viewerAllianceId` and require a nonempty matching alliance.

When true, that radar visibility predicate is omitted.

`luckyFirst` orders by cached `COALESCE(state_json.claimPriority,1) ASC`, using cache identity `(server_id, player_uid, treasure_uuid)`, then the ordinary recovered sort and stable `record_key ASC`. Treasure pages LEFT JOIN cached `state_json`, which overlays same-named base row fields.

## Sort state and native sort precision

Frontend ordered-sort toggle is exact:

- missing key -> prepend `desc` and preserve existing sorts;
- existing `desc` -> change to `asc` and move it to the front;
- existing `asc` -> remove it.

Public sort keys by kind:

- City: `level, health, shield, updatedAt`;
- Resource: `level, updatedAt`;
- Monster: `level, distance, updatedAt`;
- Truck: `quality, power, itemCount, remainingLootCount, arriveTime, updatedAt`;
- Railway: `quality, power, itemCount, protectTime, updatedAt`;
- Dispatch/Ghost: `level, quality, completionTime, updatedAt`;
- Treasure: `updatedAt`.

Exact native expressions are recovered for `updated_at`, completion time, remaining loot, protection time, arrival time, Truck special-quality CASE, Dispatch/Ghost special-quality CASE, health and current-goods item-count aggregation. Null-order fragments and stable `record_key ASC` tie-breaking are recovered.

The native `desc` branch for `distance` physically selects `ASC`, but the exact distance expression/source and reason for that inversion remain **PARTIAL**. Shield expression data flow, Railway quality branch and complete ordered multi-sort/null assembly are also **PARTIAL**. Do not invent them.

### Required parity corrections

- Reject/remove `zombie_boss` as an original public kind.
- Remove/quarantine rebuild-only Resource idle/full/black-tile filters from the 0.3.1 product surface.
- Do not expose rebuild-only Monster/Resource level semantics as original.
- Keep unresolved alternate-sort branches gated until exact native expressions are recovered.

---

# 6. City Excel — `map_city_export`

## Frontend request and visible behavior

**EXACT_BYTES.** Export exists only on City, requires a selected positive data server, and the button is disabled while a scan is reading or export is already busy.

Request:

`map_city_export(query, {headers,sheetName,yesLabel,noLabel})`

`query` is the current City query builder forced to `page=1,pageSize=200`, so current City keyword/alliance/no-alliance/marked/sort state is included.

Headers, already localized by the frontend, are exactly:

`[Server, X, Y, Player, UID, UUID, Alliance, Level, HP, Shield Ends, Marked, Updated At]`

Translation sources are `map.server`, literals X/Y, `map.player`, literals UID/UUID, `map.alliance`, `map.level`, literal HP, `automation.shieldEnds`, `map.marked`, `map.updatedAt`.

`sheetName` is localized `map.city`; `yesLabel/noLabel` are localized common yes/no.

Result fields consumed are `canceled,rowCount,path`.

- `canceled=true`: no success text/log.
- Success: visible localized success plus log `exported <rowCount> city rows to <path>`.
- Thrown error: panel error plus log `city export error ...`.

## Host validation/pagination

**EXACT_CONTRACT.** Internal export iterates pages starting at 1, page size 200, last page 1000 / maximum rows 200000. It stops when returned page rows are empty or accumulated rows >= returned `total`.

Overflow is exact:

`MAP_EXPORT_FAILED / city export exceeded the row limit`

Other recovered export errors include `MAP_EXPORT_FAILED`, `city export server is unavailable`, and `city export headers are invalid`.

## Exact row mapping

| Column | Source | Type |
|---|---|---|
| A | `serverId` | numeric |
| B | `x` | numeric |
| C | `y` | numeric |
| D | `ownerName` | inline string |
| E | `ownerUid` | inline string |
| F | `uuid` | inline string |
| G | `allianceName` | inline string |
| H | `level` | numeric |
| I | `health` | numeric |
| J | `protectEndTime`; fallback `shieldEndTime` only when primary key is absent | style-3 datetime |
| K | `marked`: JSON true -> yesLabel, otherwise noLabel | inline string |
| L | `updatedAt` | style-3 datetime |

Numeric cells accept finite JSON Number only; otherwise an empty numeric cell is emitted. Text cells accept JSON String only. UID/UUID therefore remain lossless strings.

Datetime accepts a positive finite JSON Number. Values below `100000000000` are seconds; otherwise milliseconds. Excel serial is `milliseconds / 86400000 + 25569`.

## Exact XLSX structure

Six ZIP parts only:

`[Content_Types].xml`, `_rels/.rels`, `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `xl/styles.xml`, `xl/worksheets/sheet1.xml`.

Formatting:

- columns A:L widths `11,11,11,22,24,24,18,12,12,21,10,21`;
- freeze row 1, lower pane `A2`;
- header row height 20;
- autofilter `A1:L<last-row>`;
- margins left/right .7, top/bottom .75, header/footer .3;
- four cell XFs;
- datetime format `yyyy-mm-dd hh:mm:ss`, numFmtId 164;
- header bold white on solid `004F81BD`.

Default filename is:

`map-cities-{serverId}-{YYYY}{MM}{DD}-{HH}{mm}{ss}.xlsx`

using UTC precise system time and zero-padded fields.

Native save dialog uses `Excel workbook` / `xlsx`, no custom app starting directory or title, and the normal overwrite prompt. User cancellation and recovered dialog build/show/result failure paths collapse to `{canceled:true,path:'',rowCount:0}`.

Success returns `{canceled:false,path:<selected path>,rowCount:<actual written rows>}`.

### Required parity work

The current generator explicitly removes this original API/UI and the current backend has no handler. Restore it exactly. Once the indexed City/query contract is present, this export control plane does not require protected game-side execution.

---

# 7. Scheduled Plunder control plane

## 7.1 Tab/events/list

**EXACT_BYTES / EXACT_CONTRACT.** `scheduledPlunder` is a ninth **result tab**, not a scan kind.

Tab count is `dispatchJobs.length + truckJobs.length`.

The panel subscribes to:

- `bridge://dispatch-plunder-changed`;
- `bridge://truck-plunder-changed`.

Either event refreshes `map_plunder_jobs_list()`.

`map_plunder_jobs_list` returns:

`{dispatchJobs, truckJobs}`.

Dispatch rows come from `dispatch_plunder_jobs`. Truck rows combine `truck_plunder_jobs UNION ALL truck_plunder_history`.

Ordering for both families:

1. active states `scheduled`, `waiting_connection`, `running` before terminal rows;
2. Dispatch `plunder_at ASC` / Truck `execute_at ASC`;
3. `updated_at DESC`.

Scheduler metadata overlaid into target JSON is:

`scheduleStatus, attempts, lastError, scheduledAt, scheduleUpdatedAt`, plus Dispatch `plunderAt` or Truck `executeAt` when present.

Truck result JSON can additionally carry `jobId`, `battleWon`, `plunderRewards`.

## 7.2 Dispatch schedule — `map_dispatch_plunder_schedule`

### Frontend randomization ownership

**EXACT_BYTES.** The UI random-delay input starts at string `0`; scheduling is enabled only for a safe integer >=0.

For each selected row, frontend computes:

```text
base = Number(row.plunderAt) || 0
expiry = Number(row.taskExpireTime) || 0
expiryCap = expiry>0 ? max(0,floor((expiry-base-1)/1000)) : requestedMax
safeIntCap = max(0,floor((2^53-1-base)/1000))
effectiveCap = min(requestedMax, expiryCap, safeIntCap)
randomDelaySeconds = uniform integer in [0,effectiveCap]
plunderAt = base + randomDelaySeconds*1000
```

Each emitted row adds `plunderAt,maxRandomDelaySeconds,randomDelaySeconds`. **The backend must not add another random delay.**

### Host validation

**EXACT_CONTRACT.** Payload requires a `rows` array; the complete 1..200 batch validates before the first persistence write.

Errors:

- missing rows -> `INVALID_REQUEST / secret task rows are required`;
- count outside 1..200 -> `INVALID_REQUEST / select between 1 and 200 secret tasks`;
- invalid row -> `INVALID_REQUEST / secret task scheduling data is invalid`.

Required row predicates:

- integer-like `serverId > 0` (the original wrapper does not prove an upper bound of 99999 here);
- `uuid` is a nonempty decimal-digit JSON string;
- `completionTime > 0`;
- `plunderAt >= completionTime`;
- `taskExpireTime <= 0` or `taskExpireTime > plunderAt`;
- `maxStealCount <= 0` or `stolenCount < maxStealCount`.

The integer-like accessor accepts a JSON number or signed decimal numeric string; invalid/missing converts to 0.

Persist sequentially. Exact upsert:

```sql
INSERT INTO dispatch_plunder_jobs(
  server_id,task_uuid,task_json,completion_time,plunder_at,expire_at,
  status,attempts,last_error,created_at,updated_at
) VALUES (?1,?2,?3,?4,?5,?6,'scheduled',0,NULL,?7,?7)
ON CONFLICT(server_id,task_uuid) DO UPDATE SET
  task_json=excluded.task_json,
  completion_time=excluded.completion_time,
  plunder_at=excluded.plunder_at,
  expire_at=excluded.expire_at,
  updated_at=excluded.updated_at
WHERE dispatch_plunder_jobs.status IN ('scheduled','waiting_connection')
```

A guarded write returning no row is exact:

`MAP_DATA_ERROR / scheduled plunder job is missing`.

Earlier rows remain persisted if a later row fails the guarded write. The success change event is emitted only after the full persistence loop succeeds. Native public return is the accumulated scheduled rows; the original frontend ignores that value and refreshes the list.

## 7.3 Dispatch cancel

Request: `{serverId,taskUuid}`.

Validation requires positive signed-64-bit server ID and decimal task UUID. Invalid:

`INVALID_REQUEST / server ID and secret task UUID are required`.

Cancel changes only `scheduled` or `waiting_connection` to `cancelled`, clears `last_error`, and updates time.

Missing/noncancellable:

`NOT_FOUND / scheduled plunder job not found`.

Success emits `bridge://dispatch-plunder-changed`.

## 7.4 Truck schedule — `map_truck_plunder_schedule`

**EXACT_BYTES.** Frontend sets each row:

`executeAt = max(Date.now(), Number(protectTime)||0)`

before invoking the host.

The host requires a rows array and validates the complete 1..200 batch before persistence:

- missing -> `INVALID_REQUEST / truck rows are required`;
- count -> `INVALID_REQUEST / select between 1 and 200 trucks`;
- bad row -> `INVALID_REQUEST / truck scheduling data is invalid`.

Row predicates:

- integer-like `serverId > 0`;
- `uuid` nonempty decimal-digit JSON string;
- integer-like `executeAt > 0`;
- `maxLootCount > 0`;
- `robTimes < maxLootCount` (the literal original predicate has no separate nonnegative check).

The service uses `executeAt` when present, otherwise `protectTime`; the effective value must be positive. Positive `expireAt` is persisted; absent/nonpositive becomes SQL NULL.

Service-invalid:

`INVALID_REQUEST / invalid truck plunder job`.

The batch schedules sequentially and stops on the first service/database error. Success is unit/null compatible and ignored by the frontend.

Original active-reschedule behavior:

- fresh `jobId`: `truck-{unixTimeMilliseconds}-{u64LowerHex}`;
- a prior terminal `succeeded|failed|cancelled|expired` attempt is archived;
- waiting/scheduled reschedule does not archive;
- stale `battleWon` and `plunderRewards` are removed before a new attempt;
- active upsert never replaces `running` (`WHERE truck_plunder_jobs.status<>'running'`);
- missing guarded row -> `MAP_DATA_ERROR / scheduled truck job is missing`.

The lower-hex u64 ID component is exact in shape; the original entropy generator is not recovered and must not be claimed.

## 7.5 Truck cancel and UI eligibility

Cancel request is `{serverId,trainUuid}`. Only scheduled/waiting jobs can transition. Missing job:

`NOT_FOUND / scheduled truck job not found`.

Success emits `bridge://truck-plunder-changed`.

Scheduled Dispatch columns are: Server, Owner, Quality, Rewards, Completion Time, Plunder At, Plunder Result, Actions.

Scheduled Truck columns are: Server, Player/Alliance, Quality, Plunder Count, Result, Plunder Rewards, Actions.

Cancel is shown only for `scheduled|waiting_connection`.

Truck **Plunder Again** is offered only when:

- game is online;
- job succeeded;
- target derived state is not invalid/full/expired;
- no active duplicate target exists in scheduled/waiting/running.

Truck result display consumes `battleWon` (true won / false lost), `plunderRewards`, and `lastError` for failed/expired rows.

## 7.6 Protected execution boundary

Scheduling/list/cancel persistence is recovered host control plane. Actual robbery/steal execution, game request semantics, ambiguous-send ownership and protected action internals cross the protected/game-side boundary. Preserve the recovered queue/status transitions, but do not invent missing execution behavior while the protected package is still being recovered.

### Required parity work

The current build explicitly retired this entire surface. Restore commands `map_plunder_jobs_list`, both Dispatch schedule/cancel commands, both Truck schedule/cancel commands, both change events, persistent job/history tables and the original tab/UI. Do not make Scheduled Plunder a scan kind.

---

# 8. Treasure control plane

## 8.1 Search-state refresh

Original frontend commands are:

- `map_treasure_state_refresh({serverId,records})`;
- `map_treasure_state_refresh_all({serverId})`;
- `map_treasure_claim_status()`.

Treasure page state results overlay rows by `String(uuid)`; returned state collections may be arrays or object values.

The original binary exposes read-only protected RPC names:

- `inspectTreasureStates`, with statically observed request fields `records,refresh`;
- `getTreasureClaimStatus`.

The exact host mapping of all three public refresh/status commands into every protected `inspectTreasureStates` field is **PARTIAL**. Do not substitute the current-v19 inspector's batching/validation as original unless separately tied to 0.3.1.

## 8.2 Claim request — `map_treasure_claim`

### Public request

`{serverId,claimScope,prioritizeLuckySlots,targetUuid}`.

Claim scopes are exactly `boxes`, `season`, `single`. `single` requires a nonempty target UUID.

Server range is exactly 1..99999. Invalid:

`INVALID_SERVER_ID / server ID must be an integer from 1 to 99999`.

Invalid scope/required target:

`INVALID_TREASURE_CLAIM_SCOPE / treasure claim scope is invalid`.

`prioritizeLuckySlots` defaults **true when absent**; explicit false is preserved.

### Candidate query before protected execution

**EXACT_CONTRACT.** Candidate SQL:

```sql
SELECT data_json FROM map_records
 WHERE kind='treasure' AND server_id=?1
   AND (
     COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
     OR (
       COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)=0
       AND COALESCE(CAST(json_extract(data_json,'$.complete') AS INTEGER),0)=1
     )
   )
   AND uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'
   AND (
     COALESCE(CAST(json_extract(data_json,'$.expireTime') AS INTEGER),0)<=0
     OR CAST(json_extract(data_json,'$.expireTime') AS INTEGER)>?2
   )
 ORDER BY point_index ASC
```

A separate Supplies-only query exists and must not be substituted for this claim-candidate query.

### Protected claim bridge

**EXACT_CONTRACT at host boundary.** Protected command `claimTreasures`, timeout 5,000 ms, fields:

`serverId, records, claimScope, targetUuid, prioritizeLuckySlots`.

Immediate result vocabulary:

`eligible, queued, skipped, directQueued, scoutQueued, scoutDispatched, claimed, noScoutSkipped, otherAllianceSkipped, failed`.

The public frontend consumes at least `eligible,queued,skipped`; `queued` is an admission/queue count, not authoritative completion.

### Frontend status loop

If `queued > 0`, the original frontend performs up to **1800** iterations:

1. wait 1000 ms;
2. call `map_treasure_claim_status()`;
3. merge returned states by UUID;
4. if optional `batch` exists and `batch.state` is anything other than exact `running`, stop polling.

Maximum polling window is about 1800 seconds / 30 minutes.

The protected status bridge is `getTreasureClaimStatus`, timeout 5,000 ms. Status identity vocabulary includes `playerUid,allianceId,states`; frontend additionally consumes optional `batch`.

Known frontend player-state vocabulary:

`unclaimed, dispatching, scouting, digging, claiming, claimed, failed, verifying`.

Known claim-block reasons:

`other_alliance, no_scout, no_squad, squad_reserved`.

Single claim is disabled for an empty/`0` UUID, incomplete ordinary Treasure, already-busy/claimed player state, depleted/expired world state or other-alliance block. Supported Supplies types 1/3/4 bypass the ordinary-complete gate.

## 8.3 Protected unknowns

**PROTECTED/UNKNOWN:** scope-specific candidate filtering/ordering after the host SQL, lucky-slot priority formula, duplicate suppression, Supplies scout-slot selection/reservation, and the complete hidden batch-state enumeration/transitions. Do not guess these from current Last War code.

### Required parity work

Restore public `map_treasure_claim`; restore `batch` in the claim-status result; preserve exact 1-second / 1800-iteration frontend polling. Current read-only refresh implementation can be a compatibility aid but is not proof of the original protected state derivation.

---

# 9. Dispatch Alliance Share

## Public request

**EXACT_BYTES.** `map_dispatch_share_alliance` sends only this projection for each selected row:

`uuid, serverId, x, y, cfgId, ownerName, allianceAbbr`.

## Host validation/result

**EXACT_CONTRACT**, original handler RVA `0x163EF2-0x1651C5`:

- row count must be 1..200; otherwise `INVALID_REQUEST / select between 1 and 200 dispatch tasks`;
- each `uuid` is a nonempty decimal string;
- integer-like `serverId,x,y,cfgId` must each be >0;
- `ownerName` and `allianceAbbr` are optional/nonvalidating display metadata;
- bad row -> `INVALID_REQUEST / selected dispatch task cannot be shared`.

Integer-like conversion accepts JSON number or signed decimal numeric string; finite floating input truncates toward zero; invalid/missing/overflow becomes 0.

After the **entire input validates**, rows execute sequentially through protected `shareDispatchTaskToAlliance`, with **5,000 ms timeout per row**.

A row is successful only when the protected result object contains exact boolean `shared=true`.

Public result:

`{shared, failed, sharedUuids, failedUuids}`.

The original frontend removes `sharedUuids` from current selection, displays partial/full status and logs shared plus failed counts.

## Protected boundary

Stop at `shareDispatchTaskToAlliance`. Message construction/network action/game-side authorization beyond that call is **PROTECTED/UNKNOWN**.

---

# 10. Player marks

## Public command/event

**EXACT_BYTES.** `map_player_mark_set({row,marked})`. The mark action is shown only when a City row has `ownerUid`.

The original panel listens to `bridge://player-mark-changed` only as a **refresh trigger**; it does not consume event payload fields.

## Durable identity/storage

**EXACT_CONTRACT.** Player-mark primary key:

`(server_id, owner_uid)`.

Recovered SQL:

- lookup: `SELECT state,marked_at,checked_at,player_json FROM player_marks WHERE server_id=?1 AND owner_uid=?2`;
- delete: `DELETE FROM player_marks WHERE server_id=?1 AND owner_uid=?2`;
- upsert conflict identity: `(server_id,owner_uid)`.

City search joins marks with:

`mark.server_id=page.server_id AND mark.owner_uid=CAST(json_extract(page.data_json,'$.ownerUid') AS TEXT)`.

Therefore mark identity is player/server identity, **not coordinates** and not City record key. A player can move; after rescan the new City row is marked by the same owner UID.

Server Clear deletes scan runs/map records but intentionally leaves `player_marks`, so marks survive Clear/rescan.

The exact original upsert column-value construction and public return object are not fully pinned; the frontend does not rely on direct result fields. Implement persistence identity + mark/unmark + change event exactly, and avoid inventing extra observable result requirements.

---

# 11. Original Auto Scan state machine

Auto Scan is **frontend orchestration**, not a dedicated host command.

## 11.1 Exact persisted state/defaults

**EXACT_BYTES**, original `index-sfL2sT3K.js`:

```text
enabled: false
intervalMinutes: 60
serverIds: []
selectedTypes: ['truck','railway','dispatch','ghost','treasure']
scanMode: 'fast'
returnToOriginalServer: true
nextRunAt: 0
```

Storage key: `lwbridge.mapAutoScan.<profileId>`.

Sanitization:

- interval truncates/clamps to 20..1440 minutes;
- server parser splits whitespace/comma/Chinese comma/semicolon/Chinese semicolon, accepts integer 1..99999, dedupes preserving order, maximum 20;
- selected kinds filter/dedupe through the original eight-kind set; empty result falls back to the default five;
- `scanMode` is exact `normal` only when stored value equals `normal`; otherwise `fast`;
- return-to-original defaults true unless exact false;
- `nextRunAt` becomes a nonnegative truncated number.

Enable transition false->true sets `nextRunAt=Date.now()`. Disable sets `nextRunAt=0`.

Original **Run Now is not a separate one-shot mode**: its button is disabled unless Auto is enabled, online, not cycle-busy, and not currently scanning. Clicking only sets `nextRunAt=Date.now()`.

## 11.2 Scheduler admission

The original scheduler effect lives in the top-level React app and is keyed by selected profile **and current online state**. On effect start it invokes the scheduler immediately, then every **5000 ms**.

Cycle eligibility is:

`enabled && online && !mapScanActive && !autoCycleRunning && Date.now() >= nextRunAt`.

Targets are configured `serverIds` in order. If the list is empty, use the current positive server. If neither exists, throw `MAP_AUTO_SCAN_SERVER_UNAVAILABLE`.

## 11.3 Exact cycle

1. Set cycle-running true.
2. Read current `map_scan_status`; capture original `serverId`.
3. Resolve target list.
4. For each target sequentially:
   - before target, break if effect cancelled or current config disabled;
   - await `server_jump(target)`;
   - log changed/no-op from returned `changed,previousServerId,serverId`;
   - await `map_scan_start({selectedTypes,scanMode,resume:false})`;
   - poll `map_scan_status` every **2000 ms** until `isReading=false`;
   - per-target polling deadline is **2700 seconds = 45 minutes**; timeout throws `MAP_AUTO_SCAN_TIMEOUT`;
   - if terminal status has nonempty `lastError`, log it and **continue to the next target**.
5. A thrown error from jump/start/wait escapes to the outer catch and aborts the remaining target list.
6. `finally`: if effect is not cancelled, return-to-original is enabled and original server >0, attempt `server_jump(original)`.
7. A return-jump failure is logged but does not prevent normal next-schedule calculation.
8. Clear cycle-running.
9. If effect is not cancelled, set `nextRunAt = Date.now() + intervalMinutes*60000`, sanitize/persist per profile, clear busy and refresh summary.

Effect cleanup sets cancellation true. When cancelled, the guarded return/persist path does not run.

## 11.4 Events

No dedicated Auto Scan host event exists. Auto uses ordinary host commands plus the normal `bridge://map-scan-status` stream/summary refresh. Do not add a new Auto host command or event as product behavior.

## 11.5 Required parity rollback from rebuild

The following later rebuild changes are not original 0.3.1 behavior and must be removed or quarantined for strict parity:

- removing Normal/Fast Auto control and omitting `scanMode` from start calls;
- adding `zombie_boss` to the Auto kind set;
- adding `runOnceRequestedAt` so Run Now works while recurring Auto is disabled;
- adding `lwbridge.mapAutoScanCycle.<profile>` restart-recovery markers;
- changing a thrown per-target jump/start/wait exception into catch-and-continue;
- adding reconnect/ownership semantics that alter the original profile+online effect lifecycle.

---

# 12. Current rebuild gap map for this lane

| Original control-plane surface | Strict-parity status after this recovery | Immediate implementation action |
|---|---|---|
| Manual scan kinds/mode/start envelope | **EXACT_CONTRACT** around protected acquisition | Restore eight-kind allowlist and public `scanMode`; remove Zombie Boss product kind; keep protected traversal gated |
| `map_scan_status` | **PARTIAL exact serializer, exact core/state transitions** | Match original core fields/events/source rules; do not leak rebuild-only status fields as reference |
| `map_scan_stop` | **EXACT cleanup / PARTIAL rare errors** | Return updated original state; use original idle cleanup |
| `map_scan_clear` | **EXACT_CONTRACT** | Remove serverId=0/offline saved-server Clear deviation |
| `server_jump` | **EXACT public contract / PROTECTED travel** | Restore `serverId` success field and exact public errors |
| `map_summary` | **EXACT_CONTRACT** | Remove saved-profile-index product policy; use original state/run-source selector and propagate errors |
| `map_data_options` | **EXACT envelope/source; PARTIAL scanProgress serializer** | Remove Zombie/monsterLevels additions; preserve active-run source selector |
| `map_search` | **Large EXACT subset; PARTIAL alternate-sort internals** | Remove non-reference kinds/filters; implement exact SQL branches; gate unresolved sort branches |
| City Excel | **EXACT_CONTRACT** | Restore API/UI/dialog/XLSX/pagination/row mapping |
| Scheduled Plunder list/schedule/cancel | **EXACT_CONTRACT control plane** | Restore five commands, tables/history/events/UI; keep actual protected robbery execution separate |
| Treasure claim admission/status | **EXACT host boundary + frontend polling; PROTECTED execution** | Restore public claim/status/batch control plane, stop at protected scope/lucky/scout internals |
| Alliance Share | **EXACT host validation/result; PROTECTED share action** | Restore command around protected `shareDispatchTaskToAlliance` |
| Player marks | **EXACT identity/event; PARTIAL direct result/upsert value mapping** | Restore server+owner durable mark and refresh event |
| Auto Scan | **EXACT_BYTES state machine** | Roll back rebuild workflow deviations to original timer/config/failure semantics |

---

# 13. Protected/unknown stop list

The main implementation should **not** fill these gaps from LW Atlas, current Last War behavior, or the existing rebuild:

1. `XluaBridgeMapScanTick` traversal/block acquisition order, protected extraction and retry/ack scheduling after accepted `startMapScan`.
2. Full original Map Scan public serializer field order outside the recovered frontend/native core.
3. Complete `map_data_options.scanProgress` nested serializer and absent-row representation.
4. Complete native alternate-sort SQL assembly where distance/shield/Railway-quality/null-order flow is still partial.
5. Exact original server-jump game-side travel implementation and numeric timeout duration.
6. Treasure protected scope filtering/ordering, lucky-slot algorithm, duplicate suppression, Supplies scout reservation and full batch-state enumeration.
7. Actual Dispatch/Truck plunder action execution internals after durable jobs become due.
8. Alliance Share action internals after protected `shareDispatchTaskToAlliance`.
9. Exact full state mapping inside protected Treasure `inspectTreasureStates` beyond recovered host/frontend vocabulary.

These are legitimate parity gaps pending the main researcher's protected-package/key-envelope lane.

---

# 14. Evidence index / durable locators

High-value original findings used:

- `docs/lwbridge-artifact-evidence.json` — startMapScan envelope/type filter/shared state.
- `2026-09-13-r6-map-scan-start-resume.json` — connection/duplicate/resume contract.
- `2026-09-14-r6-map-scan-world-readiness.json` — enter-world timeout/readiness/live-server gate.
- `2026-09-13-r6-map-scan-geometry.json` — 20x20 grid and size error.
- `2026-09-13-r6-map-scan-scheduler-progress.json` — exact progress counter/rate normalization.
- `2026-09-13-r6-map-scan-completion-safety.json` — native queue counters and Stop cleanup.
- `2026-09-14-r6-map-scan-event-admission.json` plus terminal/publication findings — stale event and publish gates.
- `2026-09-13-r6-map-scan-clear-ownership.json` — exact Clear ownership/server gate.
- `2026-09-09-r6-scan-state-source-selection.json` — live/none server-state logic.
- `2026-09-09-r6-map-option-source-selector.json` — staged/published selector.
- `2026-09-09-r6-map-option-response-assembly.json` — option response order/no-alliance assembly.
- `2026-09-09-r6-map-summary-native-flow.json` plus error-propagation finding — summary envelope/flow.
- `2026-09-08-r6-map-query-frontend.json` and query/filter/sort findings — exact search request/SQL.
- `2026-09-20-r7-treasure-query-state-cache.json` — foreign radar/lucky/cache SQL.
- `2026-09-19-r7-city-export-row-contract.json` plus dialog/filename/infrastructure findings — exact City Excel.
- `2026-09-19-r7-scheduled-plunder-persistence.json`, Truck public/archive findings and Dispatch schedule/public findings — exact scheduler control plane.
- `2026-09-20-r7-treasure-claim-offline-contract.json` plus frontend-status finding — Treasure claim/status boundary.
- `2026-09-20-r7-dispatch-alliance-share-offline.json` — Alliance Share host contract.
- `2026-09-08-map-index-static-recovery.json` — marks/schema/clear identity.
- immutable original `index-sfL2sT3K.js` — original Auto Scan state machine.

## Read-only note

This research report was written only under `C:\Users\chimw\OneDrive\Desktop\LW Helper Finding`. No production source, test, documentation, evidence, Git index, commit or remote branch in `LW-Control` was intentionally modified by this helper.

Final verification: branch `research/offline-controller`; `git status --short` produced no entries after this research pass.
