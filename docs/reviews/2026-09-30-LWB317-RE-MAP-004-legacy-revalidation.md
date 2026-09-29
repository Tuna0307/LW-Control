# LWB317-RE-MAP-004 — 0.3.1 hypothesis revalidation against 0.3.17

Date: 2026-09-30

## Result

State: `COMPLETE_STATIC_REVALIDATION` for the historical Map findings used by
the Phase 2 implementation. Historical 0.3.1 RVAs are rejected; every promoted
claim below is backed by current 0.3.17 bytes/control flow documented in
MAP-001 through MAP-003.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Unchanged / present in current 0.3.17

- Eight public Map data kinds: `city`, `resource`, `monster`, `truck`,
  `railway`, `dispatch`, `ghost`, `treasure`.
- Scan duplicate-start and unavailable-game gates:
  `SCAN_RUNNING` / `map scan already running` and
  `GAME_CONNECTION_UNAVAILABLE` / `game connection unavailable`.
- Request `resume` plus current-state `resumeAvailable` two-factor branch.
- All recovered current state counters and native-capture status fields except
  the frontend-only `retryCount` label.
- Current progress/unread derivation including the active 98% clamp.
- Cancel, clear, successful publication and failed-scan preservation
  transaction families.
- Base SQLite table/index families, WAL/NORMAL/foreign-key/busy-timeout setup,
  and `(kind,server_id,record_key)` / staging / mark identities.
- Search page/default/cap behavior, current public filter families, quality
  mapping, multi-sort priority, null-last behavior and stable record-key tie.
- City mark join by `(server_id,ownerUid)`.
- Current query SQL fragments for alliance, no-alliance, resource/monster name
  keys, retained-item membership, completion/plunderability, special/reindeer,
  level and treasure viewer/radar/lucky behavior.
- City export 12-column contract, page size 200, 1000-page / 200,000-row cap,
  current filename format and OOXML writer behavior.
- `serverJumpHistory` app-setting ownership and frontend list normalization.
- High-level provider entry points `enterWorldMap`, `startMapScan`, and
  `stopMapScan`.

## Changed in 0.3.17

- `map_data_options(serverId<=0)` now substitutes the shared scan-state server
  before current-source selection.
- Current Map data is explicitly profile-owned at
  `<runtime-root>/profiles/<profileId>/map-data/map-data.db`; startup also has a
  separate root-level legacy/global import source.
- Current schema version is 4. Versions below 2 run the legacy
  dispatch-assist cancellation migration before being stamped to version 4;
  versions above 4 fail with `MAP_SCHEMA_TOO_NEW`.
- Historical source marker `src\\services\\map_store.rs` is absent even though
  the current storage implementation is present and independently recovered.
- Historical lexical marker `map.records` is absent; current storage uses the
  exact SQL/table identifier `map_records`.

## Present only as current native compatibility surface

The exact current frontend uses 26 Map/server commands. The current native
dispatcher additionally registers five commands with zero occurrences across
the recovered 0.3.17 frontend assets:

| Command | Current handler | Classification |
|---|---|---|
| `server_jump_history_get` | `0x12DA36-0x12E460` | native compatibility only |
| `map_dispatch_plunder_list` | `0x12E460-0x12EDEA` | native compatibility only; UI uses unified `map_plunder_jobs_list` |
| `map_dispatch_plunder_retry` | `0x115333-0x115C5D` | native compatibility only |
| `map_truck_plunder_list` | `0x16585B-0x1661E5` | native compatibility only; UI uses unified `map_plunder_jobs_list` |
| `map_truck_plunder_retry` | `0x13D274-0x13DBB5` | native compatibility only |

They may be retained by a compatibility router, but must not be presented as
current frontend-used behavior.

`map_plunder_server_day_start` appears in provider/internal context but was not
proven as Tauri command registration and is excluded from the public command
surface.

## Absent / not promoted

- The exact executable has no recovered `retryCount` string. Frontend default
  `retryCount:2` is a presentation/default field, not a proven host persistence
  or retry contract.
- The old 0.3.1 RVAs and code addresses are all discarded.
- Historical current-client scan strategy identifiers and extra scan-run
  metadata columns from `src/LWBridge.Desktop` are not 0.3.17 base schema or
  host contracts.

## Still unknown after exact static revalidation

- a current static producer of `resumeAvailable=true`;
- game-side world traversal/block-generation details;
- retry/backoff mechanics beneath the provider boundary;
- exact normal-vs-fast game/provider execution mechanics beyond host-visible
  mode and concurrency;
- native capture queue/budget implementation details beneath the recovered
  status counters;
- absolute OS location of `<runtime-root>`;
- a product-specific SQLite corruption repair/recreate flow (none was found;
  current proven behavior is fail-closed database error propagation);
- independent native arbitrary-IPC server-history normalization beyond the
  current frontend's integer/range/unique/max-five normalization;
- exact manual-mark tracker `state`/`checked_at` policy beyond mark identity and
  true-upsert/false-delete behavior;
- provider/game effects of navigation, treasure claim/share and plunder
  execution before current-client and live validation.

## Implementation consequence

The new `src/LWBridge.Map-0.3.17/` implementation may reuse only the promoted
contracts above. `src/LWBridge.Desktop/` remains historical/current-client
evidence and is not the Phase 2 reference implementation. Unknown game-side
behavior stays behind the high-level provider adapter until compatibility/live
proof closes it.
