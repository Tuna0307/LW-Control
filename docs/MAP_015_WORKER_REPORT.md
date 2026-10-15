# MAP-015 camera-free scanner worker report — 2026-10-16

**Status: PARTIAL**

## Production implementation

- **OWN_DESIGN, current normal-world server:** all complete map scans without selected Truck try an exact-owned camera-free native AOI sweep first. The native source sends 63 sequential `WorldGetBlockMessage` requests covering 10,000 LOD0 cells (≤160 AOIs/request), reads each response's point/march data before the next request and uses the production City, Resource, Monster, Zombie Boss, Railway, Dispatch, Ghost and Treasure importers/filters, accepted identity validation, detail enrichment, resource completeness and 2,500-block publication. Fresh native City canonical-owner inconsistencies retry the **same** AOIs at most twice; each accepted request must still be complete, source-attributed and uniquely covered.
- Native cancellation and failure restore the exact original `_curViewIndex` memberships and point/world response flags. C# requires a request/session-correlated cleanup acknowledgement before a camera fallback; missing restoration is terminal. Successful full-sweep evidence requires 10,000 AOIs, exactly 63 positive replies, and 63–65 request attempts. The original camera method remains enabled for all attempted failure, timeout, empty result, and source-hydration failure cases after demonstrated native cleanup.
- Truck-containing scans use the previous qualified camera scanner. A source review found read-only queue data in `WorldMarchDataManager._messages` but no proven way to observe **every** short-lived Truck march push before the game's step queue drains it. No lossy fast Truck path was enabled; the ≤40s/non-lossy Truck goal is open. See `docs/MAP_015_BEHAVIOR_DIFFERENCES.md` for the behavior ledger.

## Exact-owned genuine native timing

These results are from isolated, unchanged-Chinese, authenticated official Windows game sessions on the current normal-world server. Each harness invocation stopped only its exact-owned game, verified original script byte restoration and left no recovery journal. The timings are **map_scan_start through the completed status**, including hydration and block publication, except where labeled native-only. Captured rows are observations that can change in the live world, not fixed population quotas.

| Kind(s) | Method/receipt | End-to-end seconds | Native AOI seconds | Records and result |
| --- | --- | ---: | ---: | --- |
| City | Camera-free `artifacts/map014-lead-live/native-benchmark-city-20261016-031852/` | 23.911 | 18.603 | 4,175; 63/63 replies, 10,000 AOIs, 0 retries, completed |
| City | Camera (same game session, next round) | 196.426 | camera producer | 4,175, completed |
| City | Optimized camera-free `artifacts/map014-lead-live/native-benchmark-city-20261016-033402/` | 20.248 | 18.008 | full direct native sweep completed, 63/63 replies, 10,000 AOIs |
| Resource | Optimized camera-free `artifacts/map014-lead-live/native-benchmark-resource-20261016-033645/` | 23.236 | 18.702 | 8,003 Resource, completeness and ~1.564s detail hydration, completed |
| Ghost | Camera-free `artifacts/map014-lead-live/native-benchmark-ghost-20261016-035108/` | **17.784** | ~17.376 managed acquisition | 142 Ghost, 63/63 replies, 10,000 AOIs, completed |
| Dispatch | Camera-free `artifacts/map014-lead-live/native-benchmark-dispatch-20261016-035248/` | **16.843** | ~16.234 managed acquisition | 355 Dispatch records, 63/63 replies, 10,000 AOIs, completed |
| Treasure | Camera-free `artifacts/map014-lead-live/native-benchmark-treasure-20261016-035350/` | **17.799** | ~17.206 managed acquisition | 22 saved Treasure records, 63/63 replies, 10,000 AOIs, completed |
| Monster | Camera-free `artifacts/map014-lead-live/native-benchmark-monster-20261016-035453/` | 27.078 | ~23.552 managed acquisition | 14,966 Monster source records, 63/63 replies, 10,000 AOIs, completed |
| Railway | Camera-free `artifacts/map014-lead-live/native-benchmark-railway-20261016-035605/` | **18.338** | direct native route | 1 authoritative Railway record, 63/63 replies, 10,000 AOIs, completed |
| Zombie Boss | Camera-free `artifacts/map014-lead-live/native-benchmark-zombie_boss-20261016-035708/` | 51.127 | ~24.740 managed acquisition | 183 Zombie Boss records, ~25.801s serial native protection-detail hydration, 63/63 replies, 10,000 AOIs, completed |
| All except Truck | Corrected camera-free `artifacts/map014-lead-live/native-benchmark-all-nontruck-20261016-033853/` | 64.055 | 26.296 | 63/63 responses, 10,000 AOIs, zero retries, **completed** and 2,500/2,500 blocks; 4,176 City, 7,998 Resource, 13,448 Monster, 186 Zombie Boss, 361 Dispatch, 144 Ghost, 19 Treasure, 0 Railway at publication |
| All except Truck | Camera-free `artifacts/map014-lead-live/native-benchmark-all-nontruck-20261016-034237/` round 0 | 69.926 | 27.352 | Completed, 10,000 AOIs, 63 requests/replies, camera checkpoints restored |
| All except Truck | Camera reference **same game session** round 1 | 225.754 | camera producer | Completed, 10,000 AOIs, camera checkpoints restored |
| All except Truck | Initial 2-round attempted A/B `artifacts/map014-lead-live/native-benchmark-all-nontruck-20261016-032544/` | 240.715 first round (fallback camera); second round failed | first direct failure after 12.518s / 4,640 AOIs | Native City canonical-owner mismatch; first run safely used camera. Second camera scan also failed on transient native City identity/restore validation; no partial publication |
| Truck | Existing camera method, MAP-014 comparison baseline | 97.887 historical Fast sample | camera | 178 Truck in archived independently owned run; new ≤40s non-lossy method unproven |
| Truck | Current camera `artifacts/map014-lead-live/native-benchmark-truck-20261016-035915/` | **204.085** | existing camera | 83 unexpired/current Truck rows, 340 camera probes, 10,000 AOIs, completed; no fast Truck claim |

The first City A/B compared actual published SQLite `record_key` sets: **4,175 camera-free vs 4,175 camera**, with one identity present only in each side (1/4,175, or ~0.024%, per direction), consistent with moving world data. Both runs restored the independent pre-run camera checkpoint exactly. Receipt: `artifacts/map014-lead-live/native-benchmark-city-20261016-031852/benchmark.json`, `benchmark-{0,1}.db` and `benchmark-{0,1}-probe-metrics.json`.

The second same-session all-non-Truck A/B used final correction code before the packaged proof. Its persisted SQLite key comparison is shown below. The number in each missing column is the number of real record keys present in that scan but absent in the other; different observations can reflect natural world motion, spawn, expiry or a source difference and **do not** by themselves prove equivalent recall. The loss criterion relative to repeated camera-vs-camera natural drift remains open for kinds without that independent control. `artifacts/map014-lead-live/native-benchmark-all-nontruck-20261016-034237/` contains the exact manifest, two databases, camera before/after checkpoints, genuine probe metrics and owned cleanup.

| Kind | Direct keys | Camera keys | Direct missing in camera | Camera missing in direct |
| --- | ---: | ---: | ---: | ---: |
| City | 4,176 | 4,176 | 1 | 1 |
| Resource | 7,997 | 7,998 | 38 | 39 |
| Monster (including Zombie Boss source rows) | 15,204 | 15,152 | 95 | 43 |
| Dispatch | 360 | 356 | 9 | 5 |
| Ghost | 144 | 143 | 1 | 0 |
| Treasure | 19 | 19 | 0 | 0 |
| Zombie Boss (separate saved kind) | 186 | 187 | 6 | 7 |
| Railway | 0 | 1 | 0 | **1** |

Both all-kind runs independently returned the **exact same original camera snapshot** after scanning, with `final.json` showing successful exact game Stop and original script restoration. The one camera-only Railway identity is an unresolved parity gap because sparse moving Railway objects were observed at different times. No guarantee of no lower recall has been asserted for that kind.

The mixed failure appeared at AOI 4,640 with `city_city_native_candidate_identity_incomplete`; the camera fallback retried similar `city_native_candidate_identity_incomplete` reports. This observation prompted an exact-chunk, maximum-two-retry direct producer correction; acceptance still rejects unresolved City identity discrepancies. The initial mixed run predates this correction, and is **not** proof of success after it.

The corrected positive mixed run above establishes full-world publication without camera movement. Its acquisition metrics record approximately 31.737s from managed acquisition start (26.296s Lua direct requests), 1.505s Resource hydration, **24.256s Monster protection/details hydration**, and 0.858s Railway refresh. Completing these record requirements pushes it far beyond the 20s all-kind target; dropping detail data just to hit the time target would fail accuracy. The 0 Railway record count is a valid observed empty source on this run, not proof of railway coverage parity with a simultaneous camera reference.

## Validation and safety

- `dotnet build tools/map_014_lead_native/Native.csproj -c Release --nologo` passed (zero warnings and errors); production UI content check passed.
- Full `tools/current_live_resource_probe.lua` parsed using the Lua 5.3 interpreter in the repository's pre-existing `artifacts/map-013-lua-site` environment.
- `python tools/check_ghost_reward_lua.py` passed (81 real installed reward config rows).
- `python -m unittest discover -s tools/map_014_lead_native -p 'test_*.py' -v` passed all 46 tests.
- `python -m unittest discover -s tools -p test_map_015_package_ui.py -v` passed all 4 new packaged-provenance/adversarial tests, including rejecting stale/mismatched native identity, duplicate AOIs and camera fallback masquerading as direct success. Inert all-nontruck source/selection positive and negative checks also passed.
- `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore` was blocked twice by a pre-tool OpenAI safety check; these tests were not run and were not rerouted.
- Read-only installed IL analysis of the Truck push processing queue found `StepProcessQueue<T>._queue` / `Queue<T>.ToArray` as a possible snapshot, but each `UpdateStep` can drain the queue on the same frame. There is no verified every-push passive subscription, and no Truck parity claim.
- Genuine native Stop test `artifacts/map014-lead-live/native-stop-20261016-034934/` interrupted the direct City scan at **8,640/10,000 AOIs** (55 requests) and returned a correlated `state=failed,error=cancelled,cameraStable=true,curViewRestored=true`. `stop-published-retention.json` proved `pass=true`: prior genuine saved data byte/record identity intact, interrupted run durably `cancelled`, no staged rows, no live scan runs, no lingering ownership and exact game/script restoration.

## Packaged Windows app and review artifact

The source-identified review ZIP is generated locally from the **clean committed HEAD**, in `artifacts/release-map015-final/LW-Control-MAP015-RC-<source-commit-prefix>.zip`; the accompanying `artifacts/release-map015-final/release-integrity.json` records the **exact archive path, full 64-digit SHA-256, Git commit, extracted file hashes, and packaged-app results**. The final archive digest cannot be hard-coded in the same Git commit used to stamp the executable's ProductVersion: modifying this committed report afterward would change the embedded build commit. The final companion integrity record is local, excluded from the ZIP and Git, and independently rehashable.

New strictly opt-in packaged proof mode `all-nontruck` uses the actual Windows Map UI to select City, Resource, Monster, Railway, Dispatch, Ghost, Treasure, and Zombie Boss (no Truck), confirms 2,500 completed blocks and same-run direct 63-reply/10,000-AOI native evidence with exact camera/AOI restore, navigates all eight tabs and checks actual source-backed saved rows. A Ghost reward UI witness verifies positive authoritative item counts shown; no separate live *plunder share* calculation was established. Packaged City and mixed proof require fresh genuine game ownership and matching real SQLite rows; fallback cannot silently pass as a direct result. Legacy packaged Home, Stop and Clear gates are retained and can be run separately.

This handback is **PARTIAL** until all acceptance gates are actually satisfied. No changes to a published release, remote user settings, official client locale or account assets were made.
