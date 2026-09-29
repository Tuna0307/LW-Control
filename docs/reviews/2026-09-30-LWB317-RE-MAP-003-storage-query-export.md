# LWB317-RE-MAP-003 — 0.3.17 Map storage/query/export contract

Date: 2026-09-30

## Result

State: `EXACT_CONTRACT` for the recovered 0.3.17 local Map data plane. This
review distinguishes exact current static behavior from provider/live behavior
and from historical 0.3.1 implementation details.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary machine-readable storage evidence:

- `evidence/lwbridge-0.3.17/map/storage-static-contract.json` —
  `374BF6313959167818FB9E710512CEA56202581DE58561FCA680AE373E23DF41`.

Query/handler provenance is also retained in the native discovery manifests
from MAP-001/MAP-002.

## Exact SQLite base schema

The 0.3.17 executable contains the current Map schema DDL, not merely table
names. The base tables are:

- `metadata(key,value,updated_at)`;
- `map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,
  level,quality,power,distance,shield_end_time,updated_at,data_json)`, primary
  key `(kind,server_id,record_key)`;
- `scan_runs(id,server_id,selected_types,status,total_blocks,
  completed_blocks,failed_blocks,created_at,updated_at,error)`;
- `scan_blocks(run_id,block_index,payload_json,status,attempts,error,updated_at)`,
  foreign-keyed to scan runs with cascade delete;
- `scan_records(run_id,kind,server_id,record_key,...,data_json)`, primary key
  `(run_id,kind,server_id,record_key)`, also cascade-owned by its run;
- `player_marks(server_id,owner_uid,state,marked_at,checked_at,player_json)`;
- `app_settings(key,value_json,updated_at)`;
- `treasure_claim_states(server_id,player_uid,treasure_uuid,expire_time,
  updated_at,state_json)`;
- `dispatch_plunder_jobs`;
- `truck_plunder_jobs`;
- `truck_plunder_history`;
- `dispatch_assist_jobs`.

Current indexes include kind/server, quality/power, level, updated time and point
indexes for published Map rows; run/kind indexing for staged rows; due-time
indexes for scheduled jobs; and expiration/update indexes for treasure/plunder
history.

Exact database pragmas present in the current executable are:

- `PRAGMA journal_mode = WAL`;
- `PRAGMA synchronous = NORMAL`;
- `PRAGMA foreign_keys = ON`;
- `PRAGMA busy_timeout = 5000`.

The current binary also contains schema-version metadata, `MAP_DATABASE_ERROR`,
`open map database`, `map database is unavailable`, the `map-data` /
`map-data.db` names, WAL checkpointing, and legacy-map-data import markers.

Current store opener `0x3DE414-0x3E085E` creates the supplied Map data
directory and appends exact `map-data.db`. Higher setup
`0x21C5B4-0x21F35C` constructs the `map-data` directory component. Therefore
the current proven suffix is `map-data/map-data.db`. The absolute parent/profile
root is not promoted from this bounded trace. A separate sibling/legacy
`map-data.db` is passed to migration code and must not be conflated with the
current directory-backed database.

Historical `src/LWBridge.Desktop/MapDataStore.cs` carries additional scan-run
metadata migrations (world/tile/current-client fields). Those columns are not
part of this recovered 0.3.17 base DDL and are not promoted as reference schema.

## Staging versus published ownership

Published data lives in `map_records`; an active run stages into `scan_records`.
Search/options/summary select the staging source only for a positive effective
server with `isReading=true`, matching state `serverId`, and a nonempty current
`scanRunId`. Otherwise they use published data.

Successful terminal publication replaces each selected kind/server from the
run's staging transactionally; failed terminal handling instead preserves
captured staged rows with `INSERT OR REPLACE` and does not perform the successful
delete-before-publish replacement. MAP-002 records the exact transaction order.

## Search pagination

Current search service is `0x3E10E7-0x3E6952`.

Native normalization is:

- page defaults/minimum to `1`;
- page size defaults to `50`;
- page size minimum `1`, maximum `200`;
- row offset is `pageSize * (page - 1)`;
- count is executed before the row query;
- row SQL ends in `ORDER BY <sorts> LIMIT ? OFFSET ?`.

The frontend may correct its page after learning `total`; no native upper-page
clamp is inferred from that UI behavior.

## Public filter families

The exact current frontend field ownership and native SQL predicates revalidate
the historical public filter families:

- keyword literal-substring search with `NOCASE` and escaping for backslash,
  `%`, and `_`;
- City alliance equality;
- City without-alliance (`NULL` or empty string);
- City marked-only through `player_marks` ownership by server/owner UID;
- Resource `resourceNameKey` JSON predicate;
- Monster `monsterNameKey` JSON predicate;
- Treasure type/supplies/radar/lucky/viewer filters;
- Truck/Railway current-goods `itemKey` through `json_each`;
- Dispatch/Ghost completion status;
- Truck/Railway/Dispatch plunderability families;
- special/reindeer filters;
- Dispatch min/max level;
- quality vocabulary `n`, `r`, `sr`, `ssr`, `ur`.

Native quality mapping is `n=1`, `r=2`, `sr=3`, `ssr=4`, `ur >= 5`. The Truck
ordinary-UR branch additionally excludes rows whose special-UR indicator is
set.

The native service contains additional internal filters not emitted by the
current Map panel. They are not promoted into extra public frontend features.

## Sorting

The current frontend still initializes every normal Map kind to
`updatedAt desc` and preserves sort-array order as priority. Native search
parses each `{sortBy,sortOrder}` and appends stable `page.record_key ASC` as a
final tie-breaker.

Recovered current sort expressions include:

- `updatedAt` -> `updated_at`;
- `level` -> `level`;
- City `health` -> JSON health coerced through `NULLIF(...,0)`;
- City `shield` -> CASE expression using `shield_end_time` then JSON
  `protectEndTime`, with current seconds/milliseconds handling;
- Monster `distance` -> stored `distance` and **forced ascending**, preserving
  the historical oddity even when a non-ascending request branch reaches it;
- Truck/Railway item-count -> aggregation over `currentGoods`;
- `completionTime`, `remainingLootCount`, `protectTime`, `arriveTime`;
- Truck quality -> special-UR rows sort as 100, otherwise stored quality;
- Dispatch/Ghost quality -> special rows sort as 100, otherwise stored quality;
- Railway quality -> plain stored quality.

The exact numeric formatter substitutions inside the City shield CASE were not
decoded in the bounded pass; the source preference and ms/sec branch are exact.
The current product frontend always supplies a usable sort; fallback
`updated_at` expression is visible natively, while its otherwise-unused
direction remains a narrow static partial.

## Summary and options

`map_summary` derives effective server from shared Map state, applies the same
active-run selector, then counts current source rows. This keeps summary counts
consistent with an in-progress matching scan without exposing unrelated
staging.

`map_data_options` is a current change from the old 0.3.1 reconstruction:
when requested `serverId <= 0`, 0.3.17 substitutes the shared-state server id
before source selection. The options aggregator returns the currently consumed
fields:

- alliances;
- resource and monster name options;
- dispatch levels;
- per-kind counts;
- no-alliance count;
- reward items;
- treasure types;
- scan progress.

Published scan progress selects the latest non-discarded `scan_runs` record for
the server ordered by `updated_at DESC LIMIT 1`; staging progress uses the
current run plus its ordered block checkpoints.

## City export

The current handler is `0x129FA7-0x12BC48`. It requires exactly 12 headers; an
invalid header vector returns `MAP_EXPORT_FAILED` /
`city export headers are invalid`. It reads `sheetName` with current fallback
`Cities`, and current fallback labels `Yes`/`No`.

It requires a positive final export server; otherwise it returns
`MAP_EXPORT_FAILED` / `city export server is unavailable`.

The save dialog is constrained to extension `xlsx` with label
`Excel workbook`. Cancel returns the normal result envelope with
`canceled=true`, zero `rowCount`, and no selected path rather than fabricating
a written file.

The current default filename is independently recovered as:

`map-cities-{serverId}-{YYYY}{MM}{DD}-{HH}{mm}{ss}.xlsx`

using UTC time with fixed zero padding. The current clock path reaches
`GetSystemTimePreciseAsFileTime` and stores zero UTC offset.

The export accumulator starts at page 1, forces page size 200, repeatedly calls
the same City search contract, stops on an empty page or when accumulated rows
reach `total`, and allows at most 1000 pages. Exceeding that bound produces
`MAP_EXPORT_FAILED` / `city export exceeded the row limit`; the resulting
current maximum is 200,000 rows.

The recovered workbook format is OOXML `.xlsx`: 12 City columns, UID/UUID kept
as text, protection/update timestamps emitted as Excel datetimes, a frozen
header row and current workbook formatting. This behavior is suitable for the
new local implementation because it is a pure data-plane/export operation.

Current cell coercion is strict rather than Excel-guessing: numeric columns
accept JSON Number values; owner/player/UID/UUID/alliance columns take the text
path. Datetimes accept positive finite numbers, treat values below
`100000000000` as seconds by multiplying by `1000`, then convert milliseconds
to Excel serial time by `/ 86400000 + 25569`. Protection prefers present key
`protectEndTime`; `shieldEndTime` is only the absent-key fallback. A present but
invalid `protectEndTime` therefore yields a blank rather than silently using
the fallback.

## Player marks

The exact schema owns marks by `(server_id, owner_uid)` and retains state,
mark/check timestamps and the player JSON snapshot. Current
`map_player_mark_set` handler `0x151985-0x1521E3` calls semantic helper
`0x42370B`. A true `marked` request takes the current upsert path; false,
missing, or non-true takes the delete path. After the store mutation it emits
`bridge://player-mark-changed`.

The current query layer independently proves the same stable identity:
marked-only tests and City result enrichment join `player_marks` by row
`server_id` plus JSON `ownerUid`, and expose stored mark state/timestamps. This
means coordinate changes/rescans do not redefine mark identity while server and
owner UID remain the same. Live relocation durability is not claimed.

Exact tracker semantics of stored `state`/`checked_at`, and tolerance of
malformed ancillary player fields beyond the required identity, remain narrow
unknowns rather than invented manual-mark policy.

## Server-jump history

Current persistent key is `serverJumpHistory` in `app_settings`. The 0.3.17
frontend imports an old local-storage list on profile activation and removes the
legacy browser key after successful import. Its normalized list is integer
server IDs `1..99999`, unique, maximum five entries.

Current native registration retains `server_jump_history_get` as compatibility
surface even though the recovered 0.3.17 frontend does not call GET. SET writes
the current app-setting upsert. IMPORT reads existing `serverJumpHistory`
first: an existing setting wins and is returned unchanged by import; incoming
history is persisted only when the setting is absent. Malformed stored JSON
produces current `INVALID_SETTING` rather than silently resetting history.

SET and IMPORT are current frontend-used commands. The independent native
normalizer for arbitrary IPC input was not fully decoded, so integer/range/
unique/max-five normalization is attributed to the exact current frontend
until a direct native caller needs wider behavior.

## Scheduled plunder and treasure state

The exact current schema proves persistent control-plane ownership for
dispatch/truck plunder jobs, truck plunder history, dispatch-assist jobs and
treasure claim state. The frontend/host command inventory proves current Map
commands to list/schedule/cancel/clear those jobs and to refresh/query/claim
treasure state.

For the local 0.3.17 implementation, persistence/list/schedule/cancel/clear
operations that are exact local data-plane behavior may be implemented from
current SQL/schema evidence. Actions that actually share with an alliance,
claim a game treasure, execute a plunder, jump/navigate, or otherwise mutate
the Last War client remain provider-adapter operations and must return an
explicit provider-unavailable result until current-client compatibility/live
work supplies that adapter.

## Corruption and database failures

The current host has explicit database-open/configuration failure vocabulary
and treats invalid persisted JSON/settings as data errors rather than silently
inventing replacement game data. The new implementation must preserve the same
fail-closed principle: schema/open/corrupt-setting failures are observable
errors, while an absent optional setting may use the exact recovered empty/
default behavior for that setting.

## Historical revalidation summary

Classified `UNCHANGED/PRESENT` in 0.3.17 after current static checks:

- base record/staging/mark/app-setting table families;
- WAL/foreign-key/busy-timeout database behavior;
- query field ownership and the public filter families above;
- page 50 / max native page size 200;
- quality mapping;
- multi-sort priority and stable record-key tie-break;
- Monster distance ascending behavior;
- Railway plain-quality sort;
- staged-vs-published active-run source ownership;
- City export 200-row paging and 200,000-row ceiling;
- server history key/list normalization vocabulary.

Classified `CHANGED`:

- `map_data_options(serverId<=0)` now substitutes shared-state server before
  source selection;
- old `src\\services\\map_store.rs` path marker is absent even though the
  current storage implementation and schema are plainly present.

Classified `ABSENT/NOT PUBLIC IN CURRENT FRONTEND`:

- `server_jump_history_get` is not invoked by the recovered frontend, although
  the native registration retains it as compatibility surface.

Still `UNKNOWN/PARTIAL` where not needed for current product behavior:

- exact no-usable-sort fallback direction;
- numeric formatter substitutions inside the City shield CASE;
- absolute parent/profile root above the proven `map-data/map-data.db` suffix;
- native arbitrary-IPC history normalization beyond current frontend input;
- exact manual-mark tracker `state`/`checked_at` policy;
- game/provider effects of treasure/plunder/navigation actions prior to the
  compatibility/live phase.
