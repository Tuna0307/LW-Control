# MAP-019 scanner method review — phase 1

Status: **IN_PROGRESS — repeated native comparisons are running.**

Base: `codex/feature-recovery-018` at `ee3ebe0221a0049512fdb7d23acb610d009db83b`.
Branch: `codex/map-scan-method-019`; draft PR [15](https://github.com/Tuna0307/LW-Control/pull/15).
Date: 2026-10-23 (Asia/Singapore). Production behavior is unchanged.

Keep the current scanner while qualification continues. No measured candidate currently
justifies a production change. This is a checkpoint, not final phase-1 acceptance.

## Completed native evidence

All paths below are private/ignored, under `artifacts/`. They contain the genuine owned-session
logs, native receipts and SQLite databases; no fixture population is used as live evidence.

| Experiment | Observation | Receipt |
|---|---|---|
| Initial City control | Two current-method rounds: 22.1977841 s and 21.4574106 s, 4,396 rows each | `map014-lead-live/native-benchmark-city-20261023-010948/final.json` and `benchmark.json` |
| Nine individual kinds, two current rounds | 18 attempted rounds. City, Resource, Monster, Secret Task, Ghost, Treasure, Railway and Truck completed. Zombie Boss on 2212 returned no confirmed rows and failed its empty-source verification; this is a retained negative, not a zero-population accuracy pass. | `map019/live-singles-audit.json`; raw `map014-lead-live/native-benchmark-review-20261023-011407` |
| Zombie Boss on owner-approved 2175 | Current: 31.804805 s / 90 rows, 35.3548011 s / 103 rows. Camera: 213.5846062 s / 93 rows, 213.6939215 s / 93 rows. Equal counts in the camera pair conceal 12 changed identities; current drift is 23 identities. Cross-method differences are 5, 13, 24 and 14; parity is not universally within both drift controls. | `map019/live-boss-2175-audit.json`; raw `map014-lead-live/native-benchmark-review-20261023-013427` |
| Opt-in coarse Boss on 2175 | 13.745269 s and 14.4247918 s, 27 rows each. All 27 are a subset of current results; 63–76 current identities are missing, exceeding drift. Reject for accuracy. Whole-world point-cell geometry does not prove all Boss/march delivery. | Same Boss audit; `map019/coarse-boss-footprint-caveat.json`, `coarse-boss-loss-pattern.json` |
| Request shape, raw City + Resource discovery | 160 areas: 22.558 s / 22.276 s; 80 areas: 42.931 s / 41.089 s. Mean 80-area time is 19.593 s (87.405%) slower. Each covers 10,000 AOIs; 63 vs 125 requests. All four have 4,395 stable City identities. This excludes Resource detail hydration and product publication. | `map019/shapes-audit.json`; raw `map014-lead-live/native-shape-review-20261023-014438` |

Every completed session above has exact owned Stop and original-script restoration.
The 2175 test returned to 2212 in 1.4833498 s and independently restored exact camera geometry.
The preferred test UID was not the authenticated account; all work remained read-only.
The owner explicitly authorized closing the existing app/game; that initial admission dependency
was resolved through normal app close and exact script recovery, not a remaining blocker.
Receipts: `map019/authorized-owner-app-close.json`, `authorized-session-recovery.json`,
`account-admission.json`. Game language remains Chinese; no desktop input or game actions.

## Current measurements and remaining work

One serial queue owns the live game at a time. It is collecting current/camera/camera/current
for the eight other individual kinds, typical City + Resource, and all nine on 2175, followed
by two all-nine home controls. Each successor requires the predecessor's exact Stop and
script restoration. Configuration and progress: `map019/queue/index.json`.
A separate opt-in Truck 20-vs-15 requests/s experiment has been built but not run.

The final report will contain one table for each assigned question, with per-kind identity,
field, timing and load results. Current Unity frame-counter and PID CPU/memory observations
are available; they are not independent display FPS or causal performance estimates.
Actual transport bytes are not measured. Local JSON/SQLite sizes are never wire bytes.

## Source-backed alternatives and reproduction

[MAP_019_SOURCE_ROUTES.md](MAP_019_SOURCE_ROUTES.md) records current installed-game source
locators for LOD, fold-up, scoped search/list/radar/minimap/event queries, Truck pushes,
remote-server parsing and incremental updates. Static scope limits are kept distinct from
native positive/negative results. Safe parallel pre-parser association, LOD1 population
coverage and complete incremental replay remain unqualified; no globally fastest or
all-alternatives-exhausted claim is justified.

[Diagnostic README](../tools/map_019_review/README.md) documents commands, exact opt-in
flags, timing scopes and restoration gates. [Request plan](../tools/map_019_review/REQUEST_PLAN.md)
contains geometry-only combined-sweep hypotheses; it sends no requests.

Validation completed at this checkpoint: 49 FEATURE018 inert suites, UI check, Desktop checks,
74 MAP014 tests, 24 MAP015 tests and 22 MAP019 diagnostic tests. Earlier shared npm-install
contention was resolved by sequential checks. Historical package test fixtures were corrected
only in tests; runtime packaging and production scanner files remain unchanged.

No merge, publication, ZIP or phase-2 default change. Final status is pending the live matrix
and interpretation; independent lead acceptance is required.
