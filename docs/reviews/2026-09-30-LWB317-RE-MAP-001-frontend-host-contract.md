# LWB317-RE-MAP-001 — 0.3.17 Map frontend/host contract

Date: 2026-09-30

## Result

State: `EXACT_CONTRACT` for the recovered frontend/Tauri surface, with native
handler entry points and provider markers recorded as `EXACT_BYTES` discovery.
This review does **not** claim live Last War behavior.

Reference executable SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Exact frontend inputs:

- `index-BVfnK1wp.js` —
  `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`;
- `MapDataPanel-B4GXEND2.js` —
  `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

Machine-readable evidence:

- `evidence/lwbridge-0.3.17/map/surface-discovery.json` —
  `E1B0801D73C546FD2C6EC422C9A4F1291C5EB0947C945DC65B5C0767FC104700`;
- `evidence/lwbridge-0.3.17/map/frontend-host-contract.json` —
  `99851E7D139B794FB219E8102F962B31EE68F0C7B5F0E11EFD50B15E8A016D98`;
- `evidence/lwbridge-0.3.17/map/native-handler-discovery.json` —
  `35D655688E86FE423419C4CE0106C2E6216C0490482F2CC6B9E190DDFAD4AAC0`.

The native-handler evidence now records the input surface-manifest SHA so the
binary -> surface -> handler provenance chain is self-contained.

## Bridge envelope

The recovered frontend uses the Tauri invoke envelope `{payload:<args>}`. For
object/null arguments the shared helper normalizes the payload and injects the
active `profileId` if it is not already present. A structured rejected value
that carries a `code` is converted into a JavaScript `Error` with the backend
fields retained.

Profile-tagged events are shaped as `{profileId,payload}` and are ignored when
the tag does not match the active profile. Untagged events pass through the
shared event helper.

Recovered Map events:

- `bridge://map-scan-status`;
- `bridge://player-mark-changed`;
- `bridge://dispatch-plunder-changed`;
- `bridge://truck-plunder-changed`.

## Current 0.3.17 command family

The exact frontend and exact PE both contain this 26-command Map/server family:

1. `map_city_export`
2. `map_coordinate_jump`
3. `map_data_options`
4. `map_dispatch_plunder_cancel`
5. `map_dispatch_plunder_clear`
6. `map_dispatch_plunder_schedule`
7. `map_dispatch_share_alliance`
8. `map_march_follow`
9. `map_player_mark_set`
10. `map_plunder_jobs_list`
11. `map_scan_clear`
12. `map_scan_start`
13. `map_scan_status`
14. `map_scan_stop`
15. `map_search`
16. `map_summary`
17. `map_treasure_claim`
18. `map_treasure_claim_status`
19. `map_treasure_state_refresh`
20. `map_treasure_state_refresh_all`
21. `map_truck_plunder_cancel`
22. `map_truck_plunder_clear`
23. `map_truck_plunder_schedule`
24. `server_jump`
25. `server_jump_history_import`
26. `server_jump_history_set`

The request/result field inventory is preserved in
`frontend-host-contract.json`. Important recovered envelopes include:

- `map_scan_start({selectedTypes,scanMode,resume?}) -> MapScanState`;
- `map_scan_status({}) -> MapScanState`;
- `map_scan_stop({}) -> MapScanState`;
- `map_scan_clear({serverId}) -> MapScanState`;
- `map_search({kind,query}) -> {rows,total}`;
- `map_data_options({serverId})` returning `alliances`, resource/monster names,
  `dispatchLevels`, `counts`, `rewardItems`, `treasureTypes`,
  `noAllianceCount`, and `scanProgress`;
- `map_city_export({query,headers,sheetName,yesLabel,noLabel}) ->
  {canceled,rowCount,path}`;
- `map_coordinate_jump({serverId,x,y})` and
  `map_march_follow({serverId,marchUuid})`;
- `map_player_mark_set({row,marked})`;
- `server_jump({serverId}) -> {changed,previousServerId,serverId}`;
- profile-scoped `server_jump_history_import/set({history})`;
- treasure refresh/claim/status commands;
- dispatch/truck scheduled-plunder list/schedule/cancel/clear commands and
  dispatch alliance share.

No `server_jump_history_get` command is present in the recovered 0.3.17
frontend. Later exact native dispatcher tracing established that GET remains a
registered compatibility command, together with four legacy per-kind plunder
list/retry commands. They are classified separately from the 26 commands the
current product frontend actually invokes; registration alone does not make
them current UI behavior.

## Scan and query frontend contract

Exact scan types are:

`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`,
`treasure`.

The frontend default scan state has server `0`, source `none`, empty run id,
`isReading=false`, phase `idle`, all eight types selected, zeroed counters,
`scanMode=normal`, concurrency `8`, retry count `2`, zero rate/progress and
zero/false native-capture counters.

Manual Start supplies `{selectedTypes,scanMode}`. Auto Scan supplies
`{selectedTypes,scanMode,resume:false}`. The frontend refreshes rows/options
when a scan changes from reading to stopped and throttles progress-driven row
refreshes to roughly one second.

The exact query vocabulary is:

`serverId`, `keyword`, `resourceNameKey`, `monsterNameKey`, `treasureType`,
`suppliesType`, `alliance`, `withoutAlliance`, `markedOnly`, `page`, `pageSize`,
`sorts`, `quality`, `specialOnly`, `reindeerOnly`, `itemKey`,
`completionStatus`, `plunderableOnly`, `includeForeignRadarTreasures`,
`luckyFirst`, `viewerUid`, `viewerAllianceId`, `minLevel`, `maxLevel`.

Normal page size is `50`; the City export request uses page size `200`. Each of
the eight normal data kinds initializes to `updatedAt desc`. The UI sort cycle
is absent -> prepend descending -> ascending -> remove, so sort-array order is
explicit priority. Current visible sort keys include `updatedAt`, `level`,
`health`, `shield`, `distance`, `quality`, `power`, `remainingLootCount`,
`arriveTime`, `protectTime`, `completionTime`, and `itemCount`.

## Auto Scan contract

Profile-scoped storage key:

`lwbridge.mapAutoScan.${profileId}`

Default configuration is disabled, 60-minute interval, no explicit servers,
types `truck/railway/dispatch/ghost/treasure`, mode `fast`, return-to-original
enabled and `nextRunAt=0`.

The exact frontend normalizes interval to `20..1440`, server IDs to unique
`1..99999` values with a maximum of 20, and selected types to the eight known
kinds. Empty server configuration falls back to the current server. The outer
scheduler cadence is about five seconds; completion polling is about two
seconds with a 2,700,000 ms bound. For each target it calls `server_jump`, then
`map_scan_start(...,resume:false)`, polls until scanning stops and optionally
returns to the original server.

## Map row actions

Coordinate navigation is guarded in the frontend against stale server rows and
requires positive integer coordinates. Truck/railway rows carrying a
`marchUuid` use `map_march_follow`; other coordinate actions use
`map_coordinate_jump`.

Player mark changes are exposed only for City rows with `ownerUid`; the mark
event invalidates/refetches the affected data.

Dispatch schedule rows receive a frontend-computed random delay constrained by
the requested maximum, task expiry and JavaScript safe timestamp range. Truck
schedule rows receive `executeAt=max(now,protectTime)`. Dispatch cancel uses
`{serverId,taskUuid}`, truck cancel uses `{serverId,trainUuid}`, dispatch clear
uses `{before,taskKind}`, and truck clear uses `{before}`.

## Native boundary recovered so far

Current 0.3.17 handler candidates are recorded in the machine-readable
contract. Key direct handlers include:

- scan start `0x143D67-0x144C41`;
- scan status `0x159925-0x15A27B`;
- scan stop `0x13A8C1-0x13B390`;
- scan clear `0x191253-0x191ACA`;
- summary `0x112B45-0x1139C4`;
- search `0x1761C1-0x176ED4`;
- options `0x16C2E0-0x16CB57`;
- city export `0x129FA7-0x12BC48`;
- player mark `0x151985-0x1521E3`.

The scan-start command calls current service function `0xFA09E-0xFC643`. That
service contains active references to the exact current errors
`GAME_CONNECTION_UNAVAILABLE`, `SCAN_RUNNING`, `INVALID_SCAN_MODE`,
`INVALID_SCAN_TYPES`, `MAP_SIZE_UNAVAILABLE`, `MAP_SCAN_START_FAILED`, and
`MAP_SCAN_REJECTED`, plus `enterWorldMap` and `startMapScan`. It also references
the current state fields `selectedTypes`, `resumeAvailable`, block counters,
scan rate/progress and the native capture ready/pending/dropped fields.

This proves the current host reaches a game/provider boundary beyond the public
Tauri command. The exact lifecycle, resume producer, capture queue, terminal
publication and retry/traversal behavior are documented separately in the scan
state-machine review as they are recovered.

## Historical 0.3.1 revalidation status

Historical RVAs are not reused. Exact 0.3.17 marker checks classify almost all
configured old hypotheses as still present at the lexical/static level. In
particular, the old alliance/no-alliance/resource-name/monster-name/item query
SQL fragments, scan duplicate/connection errors, scan state/native counter
field names, and world-map provider entry-point names are present in current
bytes.

Two configured exact old marker strings are absent/changed:

- `src\\services\\map_store.rs`;
- `map.records`.

The absence of those old strings is **not** evidence that Map storage vanished:
the exact 0.3.17 binary separately contains `map_records`, `scan_records`,
`scan_runs`, `scan_blocks`, `player_marks`, `app_settings`, and the full current
Map SQLite DDL. Semantic claims are promoted only after current control-flow or
SQL tracing.

## Phase 1 preservation

No Phase 1 UI visual campaign work was reopened. The existing
`src/LWBridge.UI-0.3.17/` clone remains the accepted static UI baseline. Any
future change there must be a narrow Map integration change justified by this
Phase 2 contract.

## Remaining boundary after this review

This review closes the frontend/Tauri inventory requirement. It deliberately
does not close:

- exact scan lifecycle/resume/terminal publication;
- exact database query and options behavior;
- exact City export writer/dialog semantics;
- exact mark/history/plunder persistence semantics;
- current-client compatibility;
- live Last War validation.

Those are subsequent Map Goal deliverables, not inferred here.
