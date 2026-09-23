# Map Data — current status

**Current through:** `LWB-R7-151`, 2026-09-24
**Canonical acceptance source:** `evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`

This is the current entry point for Manual Scan, Auto Scan, saved data, result tabs, navigation, marks, Treasure/Supplies, and current row actions. Scheduled Plunder is retired and absent from the shipped product. `docs/lwbridge-map-scan.md` remains the cumulative recovery ledger; older delivery/checkpoint prose is historical unless linked here.

## Acquisition and persistence

The ordinary shared scanner is complete for the standard current world geometry (`worldId=0`, `1000x1000`). The backend chooses the strategy; the user no longer chooses Normal/Fast.

| Area | Current status |
|---|---|
| Full-world geometry | Exact 2,500 logical blocks / 10,000 AOI cells on the standard world |
| Default mixed/full-world strategy | `current_fast_full_world_v2`, concurrency 20 |
| Truck/Railway-only | `current_fast_train_list_v1`; official `GetTrainList(true)`, zero AOI sweep; R7-150 can publish covered remote-server datasets without physical travel |
| Zombie Boss-only | `current_fast_zombie_boss_lod2_v1`, concurrency 20 |
| Nonstandard geometry | Proven fallback only for single City/Resource via `current_lod0_block_v1`, concurrency 8; unsupported combinations fail closed |
| Publication | Staged run + transactional selected-kind replacement; no partial successful publication |
| Stop | Current deterministic timing matrix plus live public Stop authority |
| Restart | Safe rejection/reconciliation; interrupted staging cannot publish; prior trusted data survives |
| Bridge loss | Definitive owned-session loss fails fast instead of fabricating failed blocks |
| Saved servers | Multi-server store/reopen/browse is proven; Auto/saved-data browsing now exposes **All** (`serverId=0`) plus each current-session saved server |
| Session lifetime | Normal app startup/teardown clears published scan data; marks/settings/jobs remain durable |
| Clear | Stopped single-server or session-wide Clear is generation-safe; Auto exposes **Clear Map Data**; active scans still reject Clear |

## Scan categories

| Category | Current status | Important limitation |
|---|---|---|
| Player City | LIVE-PROVEN complete; effective HP correction current | None known in ordinary scan/read path |
| Resource | LIVE-PROVEN acquisition/filter/sort | None known in ordinary scan/read path |
| Monster | LIVE-PROVEN; Doom Walker included with level-by-10 range such as 160/220 | Live population varies |
| Zombie Boss | Dedicated strategy LIVE-PROVEN | Population/timers vary |
| Truck | LIVE-PROVEN direct-list acquisition/goods/filter/sort; moving UUID transitions current | Truck/Railway-only scans bypass AOI; Scheduled Plunder is retired |
| Railway | Current-v20 scanner uses the official `LWTrainDataManager` Train list directly for Railway-only or Truck/Railway-only scans; historical positive acquisition/Follow remains valid provenance | Fresh population varies; the direct source itself is current-v20 recovered/live-proven |
| Dispatch / Secret Task | LIVE-PROVEN acquisition/filter/sort; R7-151 full scan uses the v21 68-request aligned wide path and exposes native Quick Find | Quick Find returns one task only; the complete scan remains authoritative |
| Ghost Ops | IMPLEMENTED and strict full-world zero-failure scans proven | Positive-row proof is owner-deferred until 2026-09-24 |
| Treasure | LIVE-PROVEN ordinary rows + read-only state refresh/cache | Public consuming Claim remains blocked/unrouted |
| Supplies | Parser/query/read-state path ready | Current 2026-09-22 scans on 2212/2213 found 0 authentic Supplies rows |

## Performance audit

The planner now has separate evidence-backed acquisition paths. Mixed scans still use exact AOI coverage, while Truck/Railway-only scans use the game-owned full list and do not sweep the map.

Representative current-v20 results:

| Scan | Older path | Current path | Coverage/result |
|---|---:|---:|---|
| Truck-only | 74.721 s AOI in R7-130 | **0.45-0.57 s source acquisition** in R7-148 | one official list refresh, 2,500 logical captures published, zero AOI requests |
| Monster | 137.036 s baseline | 77.781 s | 2,500/2,500, 0 failed/unread |
| Original all-eight selection | - | 77.890 s | 2,500/2,500, 0 failed/unread |
| Two-server all-eight | - | 82.677 s on 2212 / 78.200 s on 2213 | both complete, stored, reopened, returned to origin |

R7-150 now uses that game-owned `matchServers` coverage in Auto Scan. A current-v20 live proof stayed physically on 2212 while publishing a complete Truck/Railway dataset for covered remote server 2182 in 0.809 s: 2,500/2,500, 0 failed/unread, 2 Truck rows, `serverId=2182`, `liveServerId=2212`, `serverIdSource=remote_train_list`. The preceding coverage refresh took 0.629 s. Uncovered targets and mixed selections still use the proven jump-first path.

These are live observations, not fixed promises. World population, network/session admission, detail requests, and server state can change wall time.

R7-151 adds the current-v21 Dispatch result: the safe aligned wide footprint is 6x25 = 150 AOIs at cameraY 220, requiring 68 primary requests for the exact 10,000-AOI union. A zero-extra-startup-settle full Dispatch scan on server 2175 completed in **7.028 s** with 2 rows persisted/reopened; same-session warm scans were about **6.28-6.31 s**. City also passed the same acquisition change at 10.393 s with 94 rows. CameraY 240+ enters the unsupported split path, so 68 is the best safe camera geometry currently proven.

**Audit conclusion:** Truck/Railway-only still uses the direct Train-list route. Dispatch/Secret Task now has the fastest evidence-backed complete AOI route known here plus a separate ~0.5 s one-target native Quick Find. Quick Find does not replace completeness.

## Result/search/navigation features

Search/filter/sort/paging, saved-server browsing, result-tab persistence, mark/unmark, mark relocation after rescan/restart, coordinate Jump, moving-target Follow, Map Data Clear, and native point/march add-update-remove transitions are accepted at their recorded scopes. R7-147 corrected the owner workflow so Manual Scan has no server filter, Auto/saved-data browsing has **All**, cross-server row actions enter the row server first, and ordinary Doom Walker (`configType=8`, `configSpecial=11`) uses Follow rather than coordinate Jump.

City Excel export is intentionally **retired by owner** and is not unfinished work.

## Auto Scan

Auto Scan uses the same proven scanner rather than a second acquisition implementation. Current evidence covers ordered targets, per-target failure isolation, return to origin, Stop/disable behavior, persisted scheduling, navigation/Refresh/reconnect ownership, app-restart safe rejection/recovery, and three consecutive 2212 -> 2213 cycles with six unique complete scan legs. R7-147 separates recurring enablement from one-shot **Run now**. R7-150 adds a guarded exception to the old travel-before-scan rule: Truck/Railway-only targets covered by the current official Train-list `matchServers` snapshot scan directly without travel; uncovered or mixed targets still require confirmed travel before scan start. R7-151 adds a read-only Dispatch Quick Find immediately after confirmed travel and before the full scan; its coordinate is shown in the Auto card but is not written into complete-scan storage.

## State-changing features

These are deliberately separated from read-only Map Data correctness:

- Treasure read/state is live-proven, but the protected `claimTreasures` scope/lucky/scout scheduler remains `UNKNOWN/BLOCKED` behind the preserved SB-79 boundary. `map_treasure_claim` stays unrouted.
- R7-149 owner-retired Scheduled Plunder end-to-end: its tab, schedule/cancel commands, workers, action executors, injected game-action lanes, durable job/history tables, API wrappers, events, controls and scheduler-only locale strings are absent. Read-only Truck/Dispatch plunderability/status fields remain supported.
- Alliance-share payload/validation is offline-tested, but no real message has been sent without explicit messaging authorization.

## What still needs population or owner availability

1. Ghost positive-row proof when the event/population exists, no earlier than the owner-deferred 2026-09-24 checkpoint.
2. Supplies positive-row proof when an authentic `WorldSuppliesPoint` exists.
3. Explicitly authorized live Treasure/Alliance state-changing acceptance, with suitable expendable targets. Scheduled Plunder is retired.
4. Fresh positive current-v20 Railway row/Follow acceptance when a suitable Train is present; R7-150 coverage observed Railway population on matched server 2207, but the no-jump proof target 2182 had zero Railway rows.
5. Simultaneous real multi-account UI population if multiple live accounts/sessions become available.

The eight owner-reported Map workflow defects from 2026-09-22 are corrected in R7-147. R7-148 removes the AOI sweep from Truck/Railway-only scans, R7-150 removes physical travel for covered cross-server Truck/Railway Auto targets, and R7-151 completes the current Secret Task optimization: exact 68-request full scans plus immediate read-only native Quick Find before Dispatch Auto scans.

## Primary source trail

- `evidence/lwbridge-implementation/2026-09-24-r7-151-v21-dispatch-fast-scan.json`
- `docs/reviews/2026-09-24-r7-151-v21-dispatch-fast-scan.md`
- `evidence/lwbridge-implementation/2026-09-23-r7-train-list-no-jump-auto.json`
- `evidence/lwbridge-implementation/2026-09-23-r7-direct-train-list-speed.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-map-owner-workflow-corrections.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-map-correctness-multiserver-speed.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-native-transition-matrix.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-three-multiserver-auto-cycles.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-supplies-population-recheck.json`
- `docs/reviews/2026-09-22-r7-130-map-corrections.md` through `docs/reviews/2026-09-22-r7-147-map-owner-workflow-corrections.md`
- `docs/reviews/2026-09-23-r7-148-direct-train-list-speed.md`
- `docs/reviews/2026-09-23-r7-150-train-list-no-jump-auto.md`
