# Milestone B — exact 0.3.17 eight-kind backend matrix

The exact 0.3.17 executable proves a **generic Map record/data plane**, not an
eight-way protected game-object extractor. That distinction is the principal B
finding and prevents older provider assumptions from being smuggled into the
rebuild.

## Exact host/storage lifecycle

All eight kinds use the recovered `map_records` / `scan_records` envelope:
`kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,
power,distance,shield_end_time,updated_at,data_json`.

During an active matching run, search/options/summary may read that run's staging.
Successful completion transactionally replaces only selected kind/server published
rows from staging. Failure instead preserves staged rows with upsert semantics and
does not perform the successful delete-first replacement. Stop/cancel owns the run
and clears its staging; Clear is server-owned and refuses while a scan is active.

The exact provider boundary reaches `enterWorldMap`, `startMapScan`,
`stopMapScan` and native-capture state. The exact 0.3.17 static recovery does
**not** establish the protected per-kind Last War object extraction/serializer
implementation underneath that boundary. Those internals remain UNKNOWN rather
than inherited from 0.3.1.

## Per-kind exact query/default contract

| Kind | Exact recoverable local/query contract | Protected producer |
| --- | --- | --- |
| City | alliance equality; no-alliance is SQL NULL/empty; marks are server+owner scoped | UNKNOWN below provider |
| Resource | `resourceNameKey` JSON predicate/options; missing/null omitted | UNKNOWN below provider |
| Monster | `monsterNameKey` predicate/options; recovered forced-ascending distance family | UNKNOWN below provider |
| Truck | current-goods filter; recovered loot fallback, special/ordinary UR and quality families | UNKNOWN below provider |
| Railway | current-goods/options, arrival/plunderability and plain-quality families | UNKNOWN below provider |
| Dispatch | completion/plunderability/level rules over `completionTime/plunderAt/taskExpireTime/maxStealCount/stolenCount` | UNKNOWN below provider |
| Ghost | completion/quality query rules; exact scheduler requires pre-persist `prepareGhostPlunderTasks` | UNKNOWN below provider; action preparer handled in D |
| Treasure | type/supplies/radar/viewer/lucky queries and claim-state overlay | UNKNOWN below provider; claim/status handled in D |

The pinned DTO fixture preserves missing fields as absent and explicit JSON nulls as
nulls, then distinguishes the exact recovered COALESCE/default behavior per kind.

## Fresh B verification

- fresh hash-gated `inspect_map_surface.py`: `EXACT_BYTES_DISCOVERY`, source
  SHA-256 `4E9C...6783`;
- fresh hash-gated `inspect_map_storage_contract.py`:
  `EXACT_BYTES_STATIC_CONTRACT`, same source hash;
- Release `--map317-dto-matrix-check`: PASS for all eight kinds;
- Release `--map-campaign-canonical-check`: PASS, including staged eight-kind
  publication, profile/server isolation, persisted reopen, query/filter/sort/page,
  options, marks, fresh-process clear/rehydration, XLSX paths and inert scheduler;
- canonical campaign reported `providerMode=inert-local` and
  `externalProviderCalls=0`.

No source-backed B discrepancy was demonstrated. Therefore B makes no product
change and leaves protected producer recovery to current-v22 mapping (C) rather
than inventing exact-original internals.

## Preserved negative command

An initial read-only JSON inspection helper used Unix heredoc syntax under
PowerShell and failed in the shell parser before executing Python. It was immediately
rerun with a PowerShell here-string. No repository/product state was affected.
