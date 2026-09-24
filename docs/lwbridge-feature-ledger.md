# LWBridge current feature ledger

**Current through:** `LWB-R7-153`, 2026-09-24
This file is now a current feature ledger, not a chronological checkpoint log. Historical finding detail remains in `docs/reviews/`, subject ledgers, Git history, and machine-readable evidence.

## Home / Overview

| ID | Feature | Current evidence status |
|---|---|---|
| O01 | Detect/select valid Last War installation | PASS; current validation/persistence + historical live lifecycle |
| O02 | Launch Game | PASS; owned lifecycle and 20-cycle live stress |
| O03 | Close Game | PASS; ownership/timing matrix + live startup-close rollback |
| O04 | Launch at startup | PASS; historical live startup-reconcile + current ownership tests |
| O05 | Automatic reconnect | IMPLEMENTED/OFFLINE-TESTED with recovered thresholds/cancellation |
| O06 | Recovery/repair/error presentation | PASS at current fault/startup matrix scope |
| S01 | Game/process/bridge status | PASS; current status ownership and live authenticated bridge evidence |
| S02 | Pending count | RECOVERED and live truthfully observed at zero around current read-only RPC |
| S03 | Refresh Status | PASS by current + live composition |
| S06 | Same/cross-server navigation | LIVE-PROVEN composition |
| Home rail | Active profile/account selection | IMPLEMENTED + browser-proven; single real session transport live-proven |
| Normal Release UI | Responsive + restart/navigation | PASS_CURRENT_NORMAL_USER via R7-143 |

Current detail: `docs/tabs/home.md`.

## Map Data acquisition

| ID | Feature | Current evidence status |
|---|---|---|
| M01 | Start/Stop/type selection/backend strategy | PASS; backend auto-selects proven strategy |
| M02 | Native capture / real records | LIVE-PROVEN across populated categories |
| M03 | Typed persistent index | PASS; transactional SQLite, reopen, multi-server |
| M04 | Progress/failure/Stop/restart | PASS; current Stop, restart, bridge-loss evidence |
| M05 | Clear Map Data | PASS; server-scoped + delayed browser race proof |

## Map Data downstream features

| ID | Feature | Current evidence status |
|---|---|---|
| M06 | Per-kind result tabs/fields | PASS for populated kinds; Ghost/Supplies positive rows remain population-gated |
| M07 | Search/filter/sort/paging/live refresh | PASS for major populated families; blanket all-kind closure remains population-gated |
| M08 | Player mark/unmark | PASS; stable identity survives rescan/move/restart |
| M09 | City Excel export | RETIRED BY OWNER; intentionally removed |
| M10 | Coordinate Jump / moving Follow | PASS; live Jump/Follow authorities + explicit moving-target failure handling |
| M11 | Auto Scan | PASS; R7-151 Dispatch selections surface one native Quick Find result before the complete scan |
| M12 | Treasure read/state | LIVE-PROVEN read-only; consuming Claim remains blocked/unrouted |
| M13 | Dispatch scheduling/plunder worker | RETIRED BY OWNER R7-149; not shipped |
| M14 | Truck scheduling/plunder worker | RETIRED BY OWNER R7-149; not shipped |
| M15 | Scheduled Plunder result/status | RETIRED BY OWNER R7-149; tab/API/storage removed |
| M16 | Alliance share | Payload/validation OFFLINE-TESTED; live message not sent |

## Current scan categories

| Kind | Acquisition/read status | Remaining gap |
|---|---|---|
| City | LIVE-PROVEN | None known in ordinary path |
| Resource | LIVE-PROVEN | None known in ordinary path |
| Monster | LIVE-PROVEN including Doom Walker | Population changes dynamically |
| Zombie Boss | LIVE-PROVEN dedicated strategy | Population/timers change dynamically |
| Truck | LIVE-PROVEN | Scheduled Plunder retired; read-only status/filter fields retained |
| Railway | LIVE-PROVEN | Live population may be sparse |
| Dispatch | LIVE-PROVEN; R7-151 68-request v21 full scan + native one-target Quick Find | Quick Find is not a completeness substitute; Scheduled Plunder remains retired |
| Ghost | IMPLEMENTED; R7-152 current-v21 2,500/2,500 scans clean on five sampled servers | Positive population unavailable on 2212/2175/2180/2185/2207 at recheck time |
| Treasure | LIVE-PROVEN read-only | Claim executor blocked |
| Supplies | Parser/query/read path ready; R7-153 current-v21 2,500/2,500 scans clean on six sampled servers | Positive population unavailable on 2212/2175/2180/2185/2207/2213 at recheck time |

## Performance / strategy status

For the standard current world, production automatically chooses `current_fast_full_world_v2` at concurrency 20 for supported selections, except Zombie Boss-only which uses `current_fast_zombie_boss_lod2_v1`. Nonstandard geometry is intentionally narrower and fails closed when no proven strategy exists.

R7-151 current-v21 evidence adds a 68-request aligned Dispatch/Secret Task full-world path: server 2175 completed 2,500/2,500 logical blocks in 7.028 s with persisted/reopened rows; warm same-session scans were about 6.28-6.31 s. The v21 geometry sweep proved 6x25 = 150 AOIs at cameraY 220 is the best safe tested footprint; cameraY 240+ enters the unsupported split path. Native `DispatchFindNearestPoint` supplies one read-only target in about 0.5 s but is not a completeness substitute.

## Current evidence entry points

- `evidence/lwbridge-implementation/2026-09-24-r7-153-supplies-population-recheck.json`
- `docs/reviews/2026-09-24-r7-153-supplies-population-recheck.md`
- `evidence/lwbridge-implementation/2026-09-24-r7-152-ghost-population-recheck.json`
- `docs/reviews/2026-09-24-r7-152-ghost-population-recheck.md`
- `evidence/lwbridge-implementation/2026-09-24-r7-151-v21-dispatch-fast-scan.json`
- `docs/reviews/2026-09-24-r7-151-v21-dispatch-fast-scan.md`
- `evidence/lwbridge-implementation/2026-09-22-r7-current-evidence-index.json`
- `evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`
- `docs/tabs/home.md`
- `docs/tabs/map-data.md`
- `docs/tabs/shared-release.md`

Historical subject ledgers such as `docs/lwbridge-map-scan.md` and `docs/lwbridge-overview-recovery.md` remain source/recovery provenance and should not be interpreted as a second current status ledger.
