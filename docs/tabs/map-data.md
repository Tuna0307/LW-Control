# Map Data — current status

**Current through:** `LWB-R7-147`, 2026-09-22
**Canonical acceptance source:** `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7145.json`

This is the current entry point for Manual Scan, Auto Scan, saved data, result tabs, navigation, marks, Treasure/Supplies, and scheduled-action surfaces. `docs/lwbridge-map-scan.md` remains the cumulative recovery ledger; older delivery/checkpoint prose is historical unless linked here.

## Acquisition and persistence

The ordinary shared scanner is complete for the standard current world geometry (`worldId=0`, `1000x1000`). The backend chooses the strategy; the user no longer chooses Normal/Fast.

| Area | Current status |
|---|---|
| Full-world geometry | Exact 2,500 logical blocks / 10,000 AOI cells on the standard world |
| Default strategy | `current_fast_full_world_v2`, concurrency 20 |
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
| Truck | LIVE-PROVEN acquisition/goods/filter/sort; moving UUID transitions current | Live plunder remains separate |
| Railway | Current-v20 scanner now refreshes the official `LWTrainDataManager` Train list once per full scan and merges exact march UUID rows; historical positive acquisition/Follow remains valid provenance | Fresh R7-147 official-list probes on 2175/2180/2185/2190/2195/2196/2204 returned 0 rows, so no fresh positive current-v20 row is claimed |
| Dispatch / Secret Task | LIVE-PROVEN acquisition/filter/sort | Live plunder remains separate |
| Ghost Ops | IMPLEMENTED and strict full-world zero-failure scans proven | Positive-row proof is owner-deferred until 2026-09-24 |
| Treasure | LIVE-PROVEN ordinary rows + read-only state refresh/cache | Public consuming Claim remains blocked/unrouted |
| Supplies | Parser/query/read-state path ready | Current 2026-09-22 scans on 2212/2213 found 0 authentic Supplies rows |

## Performance audit

The current planner already uses the fastest strategy that has been proven safe for the standard world. The relevant optimization in `LWB-R7-130` stopped serializing unselected City/Resource data while keeping exact full-world coverage and the same publication/identity checks.

Representative current-v20 results from that checkpoint:

| Scan | Before | R7-130 current path | Coverage/result |
|---|---:|---:|---|
| Truck | 135.165 s | 74.721 s | 2,500/2,500, 0 failed/unread |
| Monster | 137.036 s | 77.781 s | 2,500/2,500, 0 failed/unread |
| Original all-eight selection | — | 77.890 s | 2,500/2,500, 0 failed/unread |
| Two-server all-eight | — | 82.677 s on 2212 / 78.200 s on 2213 | both complete, stored, reopened, returned to origin |

These are live observations, not fixed promises. World population, network/session admission, detail requests, and server state can change wall time.

**Audit conclusion:** there is no currently identified evidence-backed speed optimization that can be enabled without either weakening coverage/accuracy or inventing unrecovered behavior. This does **not** claim that future software can never be faster; it means the current code already selects the fastest strategy that this repository has proved safe.

## Result/search/navigation features

Search/filter/sort/paging, saved-server browsing, result-tab persistence, mark/unmark, mark relocation after rescan/restart, coordinate Jump, moving-target Follow, Map Data Clear, and native point/march add-update-remove transitions are accepted at their recorded scopes. R7-147 corrected the owner workflow so Manual Scan has no server filter, Auto/saved-data browsing has **All**, cross-server row actions enter the row server first, and ordinary Doom Walker (`configType=8`, `configSpecial=11`) uses Follow rather than coordinate Jump.

City Excel export is intentionally **retired by owner** and is not unfinished work.

## Auto Scan

Auto Scan uses the same proven scanner rather than a second acquisition implementation. Current evidence covers ordered targets, confirmed travel before scan start, per-target failure isolation, return to origin, Stop/disable behavior, persisted scheduling, navigation/Refresh/reconnect ownership, app-restart safe rejection/recovery, and three consecutive 2212 -> 2213 cycles with six unique complete scan legs. R7-147 additionally separates recurring enablement from one-shot **Run now**: a user may run the configured multi-server cycle while recurring Auto is unchecked, without silently enabling future schedules.

## State-changing features

These are deliberately separated from read-only Map Data correctness:

- Treasure read/state is live-proven, but the protected `claimTreasures` scope/lucky/scout scheduler remains `UNKNOWN/BLOCKED` behind the preserved SB-79 boundary. `map_treasure_claim` stays unrouted.
- Truck/Dispatch schedule/cancel/worker/restart behavior is implemented/offline-tested, but no real live plunder outcome is claimed.
- Alliance-share payload/validation is offline-tested, but no real message has been sent without explicit messaging authorization.

## What still needs population or owner availability

1. Ghost positive-row proof when the event/population exists, no earlier than the owner-deferred 2026-09-24 checkpoint.
2. Supplies positive-row proof when an authentic `WorldSuppliesPoint` exists.
3. Explicitly authorized live Treasure/Truck/Dispatch/Alliance state-changing acceptance, with suitable expendable targets.
4. Fresh positive current-v20 Railway population/Follow when an authentic Train is present. The source defect is corrected; current sampled official lists were empty.
5. Simultaneous real multi-account UI population if multiple live accounts/sessions become available.

The eight owner-reported Map workflow defects from 2026-09-22 are corrected in R7-147. No additional ordinary Manual/Auto implementation defect is currently known; the fresh Railway positive row is an availability verification gap, not claimed as passed by R7-147.

## Primary source trail

- `evidence/lwbridge-implementation/2026-09-22-r7-map-owner-workflow-corrections.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-map-correctness-multiserver-speed.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-native-transition-matrix.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-three-multiserver-auto-cycles.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-supplies-population-recheck.json`
- `docs/reviews/2026-09-22-r7-130-map-corrections.md` through `docs/reviews/2026-09-22-r7-147-map-owner-workflow-corrections.md`
