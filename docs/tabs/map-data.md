# Map Data — current status

**Current through:** `LWB-R7-149`, 2026-09-23
**Canonical acceptance source:** `evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`

This is the current entry point for Manual Scan, Auto Scan, saved data, result tabs, navigation, marks, Treasure/Supplies, and current row actions. Scheduled Plunder is retired and absent from the shipped product. `docs/lwbridge-map-scan.md` remains the cumulative recovery ledger; older delivery/checkpoint prose is historical unless linked here.

## Acquisition and persistence

The ordinary shared scanner is complete for the standard current world geometry (`worldId=0`, `1000x1000`). The backend chooses the strategy; the user no longer chooses Normal/Fast.

| Area | Current status |
|---|---|
| Full-world geometry | Exact 2,500 logical blocks / 10,000 AOI cells on the standard world |
| Default mixed/full-world strategy | `current_fast_full_world_v2`, concurrency 20 |
| Truck/Railway-only | `current_fast_train_list_v1`; one official `GetTrainList(true)` refresh, zero AOI sweep |
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
| Dispatch / Secret Task | LIVE-PROVEN acquisition/filter/sort | Scheduled Plunder is retired; read-only eligibility/status fields remain |
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

The live R7-148 Train-list response from server 2212 also exposed game-owned `matchServers` coverage for 2182, 2193, 2197, 2198, 2204, 2207, 2208, 2209 and 2212 while returning Truck rows from only a subset. This proves the underlying list is cross-server and distinguishes covered-empty servers from servers outside the match set. Auto Scan has **not yet** been changed to skip travel based on this coverage; that remains a separate implementation step.

These are live observations, not fixed promises. World population, network/session admission, detail requests, and server state can change wall time.

**Audit conclusion:** for Truck/Railway-only scans, the direct Train-list route is now the fastest evidence-backed safe path known in this repository. Other categories remain on their current proven strategies until an equally authoritative direct query/list path is recovered and validated.

## Result/search/navigation features

Search/filter/sort/paging, saved-server browsing, result-tab persistence, mark/unmark, mark relocation after rescan/restart, coordinate Jump, moving-target Follow, Map Data Clear, and native point/march add-update-remove transitions are accepted at their recorded scopes. R7-147 corrected the owner workflow so Manual Scan has no server filter, Auto/saved-data browsing has **All**, cross-server row actions enter the row server first, and ordinary Doom Walker (`configType=8`, `configSpecial=11`) uses Follow rather than coordinate Jump.

City Excel export is intentionally **retired by owner** and is not unfinished work.

## Auto Scan

Auto Scan uses the same proven scanner rather than a second acquisition implementation. Current evidence covers ordered targets, confirmed travel before scan start, per-target failure isolation, return to origin, Stop/disable behavior, persisted scheduling, navigation/Refresh/reconnect ownership, app-restart safe rejection/recovery, and three consecutive 2212 -> 2213 cycles with six unique complete scan legs. R7-147 additionally separates recurring enablement from one-shot **Run now**: a user may run the configured multi-server cycle while recurring Auto is unchecked, without silently enabling future schedules.

## State-changing features

These are deliberately separated from read-only Map Data correctness:

- Treasure read/state is live-proven, but the protected `claimTreasures` scope/lucky/scout scheduler remains `UNKNOWN/BLOCKED` behind the preserved SB-79 boundary. `map_treasure_claim` stays unrouted.
- R7-149 owner-retired Scheduled Plunder end-to-end: its tab, schedule/cancel commands, workers, action executors, injected game-action lanes, durable job/history tables, API wrappers, events, controls and scheduler-only locale strings are absent. Read-only Truck/Dispatch plunderability/status fields remain supported.
- Alliance-share payload/validation is offline-tested, but no real message has been sent without explicit messaging authorization.

## What still needs population or owner availability

1. Ghost positive-row proof when the event/population exists, no earlier than the owner-deferred 2026-09-24 checkpoint.
2. Supplies positive-row proof when an authentic `WorldSuppliesPoint` exists.
3. Explicitly authorized live Treasure/Truck/Dispatch/Alliance state-changing acceptance, with suitable expendable targets.
4. Fresh positive current-v20 Railway dataset/Follow acceptance when a suitable Train is present on the actively validated target server. R7-148 now sees authentic Railway rows in the global official Train list on matched servers, so the old all-empty population statement is retired.
5. Simultaneous real multi-account UI population if multiple live accounts/sessions become available.

The eight owner-reported Map workflow defects from 2026-09-22 are corrected in R7-147. R7-148 additionally removes the AOI sweep from Truck/Railway-only scans. Cross-server no-jump publication and Secret Task fast lookup remain separate optimization work, not regressions in the ordinary scanner.

## Primary source trail

- `evidence/lwbridge-implementation/2026-09-23-r7-direct-train-list-speed.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-map-owner-workflow-corrections.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-map-correctness-multiserver-speed.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-native-transition-matrix.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-three-multiserver-auto-cycles.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-supplies-population-recheck.json`
- `docs/reviews/2026-09-22-r7-130-map-corrections.md` through `docs/reviews/2026-09-22-r7-147-map-owner-workflow-corrections.md`
- `docs/reviews/2026-09-23-r7-148-direct-train-list-speed.md`
