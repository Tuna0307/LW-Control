# Four genuine packaged Truck-only runs: old versus refactored collector

**Disposition: source/coverage acceptance in all four retained runs; no demonstrated material full-sweep performance improvement.** This is a **read-only, sanitized post-hoc analysis** of saved packaged UI/native evidence, not a new live execution, a simulated performance measurement, a change to native request pacing, or a comparison with a remote scan service. Full machine-readable aggregate counts, source pins, source-file references and timings are in [TRUCK_ONLY_FOUR_RUN_PACKAGED_COMPARISON.json](TRUCK_ONLY_FOUR_RUN_PACKAGED_COMPARISON.json).

## Source-pin and scope

The four retained folders are:

| Variant | Run (2026-10-24) | Retained package collector source SHA-256 prefix | Native client | UI selection |
|---|---|---|---|---|
| Old | `083804` | `084c2dbe77659053` | v27 | Truck only |
| Old | `084050` | `084c2dbe77659053` | v27 | Truck only |
| Refactored | `085052` | `0b47adb3e7a8ffd1` | v27 | Truck only |
| Refactored | `085312` | `0b47adb3e7a8ffd1` | v27 | Truck only |

Each run's source pin comes from its retained `overview-evidence/<session>/helper-start.json`, not a presently edited checkout. The two old packages have the same probe source and packaged-probe fingerprints; both new packages have a different, internally identical pair. The packaged Desktop bridge source fingerprint is the **same in all four**. All four use profile `map014-native-ui`, server **2212**, world **0**, the same input-map-source fingerprint, genuine game provider rather than a controlled fixture, and packaged UI `truck` selection. Saved `native-ui-map.json` states are proven, with **2,500/2,500 UI blocks, zero failed/unread blocks, zero native pending/dropped rows**. Saved `owned-exit.json` reports exit code 0, all original installed scripts restored and no outstanding recovery journal. Raw machine paths, PIDs, owner identities, account IDs, challenges, tokens and march UUIDs are deliberately **not** reproduced here or in the companion JSON.

## Native coverage, repairs and end-to-end timing

All four native receipts report `state=proven`, **625/625** rectangles, **0 unproven**, **625 initial requests**, 20 requests/s, sequential **fences of 25**, no Railway selection, stable camera and exact view restoration. Retries are additional to the initial requests.

| Run | Version | Repairs | Requests sent | Pushes seen | Native Truck elapsed | Reply wait (subphase) | Native typed Truck rows |
|---|---|---:|---:|---:|---:|---:|---:|
| `083804` | Old | 25 | 650 | 648 | **51.769 s** | 45.866 s | 831 |
| `084050` | Old | 100 | 725 | 721 | **78.612 s** | 63.956 s | 803 |
| `085052` | New | 0 | 625 | 625 | **38.666 s** | 32.611 s | 744 |
| `085312` | New | 25 | 650 | 649 | **51.088 s** | 41.558 s | 757 |

The meaningful *repair-workload-matched* observation is old `083804` versus new `085312`: **51.768834 versus 51.087870 seconds, saving just 0.680964 seconds (1.315%)**. Both sent 650 requests, used 25 repairs in one pass, and completed 625 rectangles with the same pacing/fence contract. This is a **negligible observed full native sweep advantage**, not a stable/qualified speedup (one pair, sequential in time, different moving population). Native reply waits changed by more than the total result, so neither the wait difference nor the total result isolates the collector's CPU impact.

The tempting comparison of old `084050` (78.612 seconds) to new `085052` (38.666 seconds) is **not a matched collector performance test**: the old run needed **100 repairs** and the new run **zero**. Other old/new combinations have similarly uncontrolled repair counts and changing source rows. Averaging both old times against both new times would improperly attribute native reply/retry variance to the Lua collector; **do not report a 30–50% new-scanner gain** on that basis.

### Saved engine stage timings

These are values from each run's own `live-resource/map-engine-stage-metrics.json`; they are **not** independent process CPU measurements.

| Run | Source capture | Checkpoint | Publication | Whole engine |
|---|---:|---:|---:|---:|
| Old `083804` | 55.214 s | 0.035 s | 0.063 s | 55.328 s |
| Old `084050` | 82.232 s | 0.058 s | 0.093 s | 82.406 s |
| New `085052` | 42.219 s | 0.059 s | 0.053 s | 42.343 s |
| New `085312` | 54.657 s | 0.058 s | 0.076 s | 54.796 s |

In the 25-repair pair, source capture differs by approximately **0.556 s** and the whole engine by **0.532 s**. These phases include native collection and other pipeline/handshake work and cannot identify a Lua collector-only saving. All native `captureSeconds` fields equal **0.0**, a within-frame timer limitation rather than proof that collection costs zero CPU.

**CPU and peak memory:** these four retained native, UI, stage and owner receipts do **not** contain attributable CPU seconds or peak process working-set/RSS measurements. Those values are explicitly `null` in the JSON. Process timing cannot be derived from wall time and peak RAM cannot be derived from row or file size. Unrelated runs or a synthetic microbenchmark are not substituted.

## Typed native fields, movement, arrival and display

Each retained `trainRecords` array has precisely as many records as its native `trainRecordCount` header, with **831/803/744/757 distinct march UUIDs** respectively. Every row in each run has the same **33 source keys**, including source and runtime class, world/server/position, per-row `capturedAtMs`, Truck `trainType`, `trainCfgId`, `trainQuality`, `trainUuid`, `truckMetadataKnown`, goods arrays, max-loot, owner and alliance fields, `endTime` and `arriveTs`. All rows are genuine `WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train` observations, `trainType=1`, `serverId=2212`, `worldId=0`, have `truckMetadataKnown=true`, nonempty native current-goods arrays, and agree with the source world's 1,000-wide 1-based tile index. All four have **zero** per-row records already past either expiry boundary at their individual `capturedAtMs`.

However, by each terminal native receipt's UTC timestamp, **53 / 101 / 33 / 47** previously valid Truck records, respectively, had passed their `endTime`. This is a time-separated expiry observation, not proof that they were admitted as already expired at capture. The packaged UI's unfiltered browsable totals (**774 / 701 / 708 / 710**) are lower than raw native counts; the terminal-expiry counts help explain differences, but do not alone prove the entire projection policy. The saved UI filtered totals (**453 / 237 / 231 / 239**) also cannot be used as correctness/performance equivalence: the saved reward-filter choices were **not identical in all four runs**.

Source-object motion further defeats direct row-count comparison. Among the **519** common Truck UUIDs in the matched 25-repair pair, **519/519** had changed point positions and **517/519** had changed `endTime`; the old/new result sets contain **312 and 238** other UUIDs, respectively. Other adjacent run pairs likewise exhibit moving shared UUIDs and changing end times (counts in the JSON). Native source-record equality between runs is neither expected nor a valid coverage acceptance criterion for this live game.

## Qualification decision and remaining proof

**Retain the refactor as a collector-local optimization with complete inert field-parity support and successful packaged 625/625 acceptance; do not claim a substantial Truck-only native or UI wall-time win from these four runs.** The one balanced 25-repair pair is essentially tied. Genuine comparative speed qualification would need multiple alternating old/new trials matched for repair count and request timing, current-world dynamic cohorts, same filter selections, packaged source-pinned identity, exact 625/625 and typed-field assertions, restoration, and attributable per-process CPU/peak-memory telemetry if those metrics are to be published.

Evidence for each run: `artifacts/map014-takeover/native-ui-20261024-<run>/live-resource/fast-truck-scan-result.json` (native census/protocol/typed rows); `native-ui-map.json` (UI completion/filter); `live-resource/map-engine-stage-metrics.json` (capture/checkpoint/publication); `source.json` (fixture provenance); `overview-evidence/<owned-session>/helper-start.json` (packaged source pin); `owned-exit.json` (restoration). The retained files contain game/user details; rely on the **sanitized** aggregate JSON for review or sharing.
