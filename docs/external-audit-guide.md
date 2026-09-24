# External AI audit guide

Use this document when handing the repository to another AI or reviewer. It is intentionally short and points to the current sources of truth instead of asking the auditor to reconstruct status from hundreds of chronological files.

## Read in this order

1. `AGENTS.md` — mandatory evidence/recovery/safety/delivery rules.
2. `docs/README.md` — canonical documentation map.
3. `docs/tabs/home.md` — current Home / Overview status.
4. `docs/tabs/map-data.md` — current Map Data status and scan-performance audit.
5. `docs/tabs/shared-release.md` — shared runtime and Release status.
6. `docs/lwbridge-project-status.md` — current project-manager summary.
7. `evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json` — current 47-case status after the R7-149 owner retirement of Scheduled Plunder.
8. `evidence/lwbridge-implementation/README.md` and the current evidence index — curated evidence navigation.

## Audit method

Do not treat the newest-looking sentence in an old checkpoint as current status. This repository preserves superseded findings on purpose. Resolve conflicts by date/checkpoint and by the current acceptance matrix.

Check claims at their stated evidence scope:

- `RECOVERED` is static/original-artifact evidence, not live proof.
- `IMPLEMENTED/OFFLINE-TESTED` proves the rebuild/tests, not a live outcome.
- `LIVE-PROVEN` requires current-client runtime evidence.
- composed statuses explicitly combine current deterministic evidence with historical live authority.
- `UNKNOWN/BLOCKED` must remain blocked rather than being filled with plausible behavior.

## High-value code paths to inspect

- `src/LWBridge.Desktop/OverviewLifecycleService.cs`
- `src/LWBridge.Desktop/ManualMapScanCommandService.cs`
- `src/LWBridge.Desktop/MapScanContract.cs`
- `src/LWBridge.Desktop/CurrentClientMapBlockSource*.cs`
- `src/LWBridge.Desktop/MapScanEngine.cs`
- `src/LWBridge.Desktop/MapDataStore*.cs`
- `src/LWBridge.Desktop/LWBridgeBackend.cs`

## Claims worth challenging

An auditor should specifically verify:

- the standard-world planner really selects Fast/concurrency 20 and does not expose a slower user mode;
- exact full-world coverage is preserved by the R7-130 optimization;
- Player City effective HP does not regress to stale raw current HP;
- generic Monster still includes ordinary Doom Walker and level-by-10 variants;
- Truck/Railway moving identity uses exact march UUID and does not duplicate moved rows;
- current-v20 Railway performs the official `LWTrainDataManager.TryGetTrainList(true)` refresh once per full scan rather than relying only on world marches;
- covered Truck/Railway-only Auto targets use `matchServers` + `targetServerId` without physical travel, while mixed/uncovered targets retain jump-first behavior and `liveServerId` remains the physical-server authority;
- Manual has no server filter while Auto/saved-data browsing has **All** + saved servers;
- one-shot Run Now works with recurring Auto disabled and does not enable future scheduling;
- cross-server row Jump/Follow enters the row server before navigation;
- Auto Scan cannot duplicate a due cycle across navigation/Refresh/reconnect/restart;
- interrupted scans cannot publish partial staging over trusted data;
- Clear cannot resurrect stale search results;
- normal Release Home/Map navigation is not fixture-only.

## Known remaining gaps — do not report these as newly discovered defects

- Ghost positive-row population: R7-152 current-v21 five-server recheck completed clean scans but found zero authentic rows; keep this as a population gate, not a scanner defect.
- Supplies positive-row population: R7-153 current-v21 six-server recheck completed clean scans but found zero authentic `WorldSuppliesPoint` rows; keep this as a population gate, not a scanner/parser defect.
- Fresh current-v20 Railway positive row: official-list probes on 2175/2180/2185/2190/2195/2196/2204 were authoritative but empty in R7-147; do not misreport that as a source failure or as a fresh positive pass.
- Treasure protected claim scheduler: `UNKNOWN/BLOCKED` behind the preserved SB-79 boundary; public claim is intentionally unrouted.
- Simultaneous real multi-account UI population: target availability gap.
- Final integrated release acceptance remains a separate release-level gate even though ordinary technical `partial` rows are zero.

## Historical material

`docs/reviews/`, `docs/lwbridge-map-scan.md`, `docs/lwbridge-overview-recovery.md`, `docs/lwbridge-injection.md`, and older evidence JSON/TXT files are retained to make prior claims reproducible. They should not be deleted merely because their old status language is superseded.

Current speed proof: `docs/reviews/2026-09-23-r7-148-direct-train-list-speed.md`.
Current feature-retirement proof: `docs/reviews/2026-09-23-r7-149-scheduled-plunder-retirement.md`.
Current no-jump speed proof: `docs/reviews/2026-09-23-r7-150-train-list-no-jump-auto.md`.
Current Ghost population proof: `docs/reviews/2026-09-24-r7-152-ghost-population-recheck.md`.
Current Supplies population proof: `docs/reviews/2026-09-24-r7-153-supplies-population-recheck.md`.
