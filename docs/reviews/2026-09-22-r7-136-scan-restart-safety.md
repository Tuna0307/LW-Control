# LWB-R7-136 — Manual and Auto Scan restart safety

**Date:** 2026-09-22
**Base revision:** `d656c043a9d14d535fbcf9ec5bdc5186b6dad159`
**Scope:** close B08/D04 restart safety at current offline/browser scope without inventing scan resume semantics or claiming an intentional live-game process kill.

## Finding R7-136-F1 — interrupted Manual scans could remain durably `running`

The persistent scan store can outlive the application process. Before R7-136, an abrupt process loss after a checkpoint could leave the latest `scan_runs` row in `status=running`. A new `ManualMapScanCommandService` starts from idle and public `resumeAvailable` is deliberately false, so retaining the old durable `running` state was neither truthful resume nor explicit rejection.

### Production policy

R7-136 adds `MapScanProcessLease`, an exclusive per-profile scan-owner lease adjacent to the profile's `map-data.db`.

- The lease is acquired before a fresh scan is admitted and is held until terminal completion/Stop/failure.
- A second LWBridge process cannot acquire the same profile lease and receives the existing `SCAN_RUNNING / A map scan is already in progress.` contract rather than altering another owner's scan.
- Startup/restart reconciliation occurs only while the process owns the exclusive lease.
- Any persisted `running` row that exists after exclusive ownership is acquired is necessarily orphaned by the prior owner and is changed to:
  - `status=failed`
  - `error=map scan interrupted by application restart`
- The interrupted run's durable checkpoints and staging remain available as interruption evidence.
- The old staged rows cannot publish because publication requires the run to still be `running`.
- Previously published trustworthy data is not replaced.
- Public resume remains unsupported and `resumeAvailable=false`.

For `:memory:` tests, the same ownership semantics use a store-local semaphore instead of a filesystem lock.

## Deterministic restart matrix

The file-backed regression deliberately creates a partially checkpointed scan and then disposes the database without Stop/Fail/Publish, modelling abrupt process loss after durable checkpointing.

Observed/required behavior:

1. Before reconciliation the reopened store still reports the orphaned run as `running`.
2. Construction of the next exclusive scan owner changes it to `failed` with the restart reason.
3. Its one completed checkpoint remains present.
4. The pre-existing published City row remains visible.
5. The interrupted run's staged City row never becomes published.
6. A direct attempt to publish the reconciled run fails with exact `INVALID_SCAN / map scan is not running`.
7. While the first post-restart service owns the lease, a second service opening the same database does **not** reconcile that live run and cannot Start over it.
8. After the first owner Stops, the second service can start a **fresh** run, still with `resumeAvailable=false`.

This satisfies B08's required “compatible resume **or explicit safe rejection**” branch through explicit safe rejection.

## Finding R7-136-F2 — Auto cycle due-time alone could re-admit a crashed cycle

The Auto scheduler durably stored `nextRunAt`, but before R7-136 it did not persist a distinct in-flight cycle identity.

If the application disappeared after entering a target server but before the scheduler's `finally` advanced `nextRunAt`, a new application instance could see the same Auto configuration as still due. Because public scan resume is intentionally unsupported, silently restarting that old target list would be the wrong restart policy.

### Production Auto restart policy

The generated top-level scheduler now persists:

`lwbridge.mapAutoScanCycle.<profileId>`

with schema version 1, cycle `startedAt`, authoritative `originalServerId`, and the cycle's `returnToOriginalServer` setting.

The marker is written **before target travel**.

On a later application instance the scheduler checks restart recovery before ordinary due-cycle admission:

- the interrupted target list is never resumed;
- no replacement `map_scan_start` is admitted from that marker;
- when return-to-origin is enabled, the scheduler restores the authoritative original server first;
- if restoration fails, the marker remains and recovery retries on a later scheduler tick without admitting a scan;
- only after successful restoration does it advance `nextRunAt` and clear the marker;
- if return-to-origin was disabled, restart preserves the current server, advances `nextRunAt`, clears the marker, and still does not resume the interrupted target list.

Normal-cycle marker cleanup is also conditional on successful return-to-origin, so a failed normal-cycle return remains visible/retryable instead of being silently forgotten.

## Full browser-context restart acceptance

The permanent `tools/check_map_auto_r7136_restart.cjs` closes and recreates an entire browser context using persisted storage state, representing the frontend/application lifetime boundary rather than merely navigating between React views.

### Return-to-origin enabled

First lifetime:

- home server: 2212
- configured targets: 2213, 2214
- first target 2213 entered
- exactly one `map_scan_start` admitted
- persisted marker records original server 2212
- `nextRunAt` is still 0 when the first context disappears

Second lifetime begins on server 2213:

- the first synthetic restoration attempt to 2212 is deliberately failed;
- the marker remains;
- replacement `map_scan_start` count remains 0;
- the next recovery tick retries 2212;
- authoritative current server becomes 2212;
- the marker clears only after restoration;
- `nextRunAt` moves to the future;
- replacement `map_scan_start` count remains 0.

### Return-to-origin disabled

After the same interrupted first-target setup:

- restart begins on server 2213;
- no `server_jump` is issued;
- no `map_scan_start` is issued;
- current server remains 2213;
- marker clears;
- `nextRunAt` moves to the future.

This closes D04's application/process restart branch at current browser/offline scope. R7-131/R7-132 already cover Stop, disconnect/reconnect and duplicate-cycle ownership.

## Regression integration

R7-136 adds/updates:

- `src/LWBridge.Desktop/MapScanProcessLease.cs`
- `ManualMapScanCommandService` lease acquisition/release and startup reconciliation
- `MapDataStore.ReconcileInterruptedEngineScans`
- file-backed deterministic restart/concurrent-owner coverage
- canonical frontend generator Auto restart marker/recovery logic
- generated shipped frontend bundle
- `tools/check_map_auto_r7136_restart.cjs`
- `tools/check_scan_strategy_auto.cjs` source-contract guard
- CI execution of the new R7-136 browser regression

## Validation

Fresh clean-worktree validation:

- canonical frontend generator check — **PASS**
- existing R7-131 browser regression — **PASS**
- existing R7-132 scheduler regression — **PASS**
- new R7-136 full-context restart regression — **PASS**
- full frontend browser suite — **36 checks PASS**
- City export removal browser regression — **PASS**
- visual comparison — **32 pairs PASS**
  - 28 strict pairs
  - initial full-suite Hotkeys dark zh-CN capture had MAE `3.4106877310740937e-06`, high-delta rate 0
  - isolated recapture returned exact equality
  - final strict result: **28/28 pixel-identical**, strict max MAE 0, strict max high-delta rate 0
  - four intentional Map Data override pairs remain within existing bounds
- Release build — **0 warnings / 0 errors**
- all six deterministic groups — **true**, `failures=[]`
- normal production Overview/Map Data passive smoke — **PASS**
- final LWBridge/game/launcher/helper process counts — 0

Pre-commit candidate worktree Release executable SHA-256 (Git metadata still at base revision `d656c043…`):

`1D087D4D08813F5E045DE2A3CF91DCD03C2E7B7A14F92048CDB6A06B0B655CE7`

Exact staged index-only Release executable SHA-256:

`8A228D9AD619BB9406855A8CEBAECA3E10E397102B5153569CA43E50DE24A489`

The staged snapshot intentionally has no `.git` metadata, so the .NET SDK omits `SourceRevisionId` from `AssemblyInformationalVersion`; the application source is identical.

## Acceptance effect

- **B08:** **PASS_CURRENT_OFFLINE** — explicit safe rejection after interrupted scan, durable checkpoint preservation, no stale publication, no concurrent-owner takeover.
- **D04:** **PASS_CURRENT_OFFLINE** — interrupted Auto cycle never resumes, server restoration is truthful/retryable, and the next cycle is scheduled only after recovery.

## Limits

R7-136 does **not** claim an intentional OS-level live-game process kill while a real map scan is active. That would be stronger live evidence, but it is no longer an identified implementation gap.

Still open independently:

- D02's requested set of at least three authorized full multi-server Auto cycles;
- Ghost positive-row proof;
- Supplies positive-row proof;
- explicitly authorized state-changing Truck/Dispatch/Alliance/Treasure acceptance;
- simultaneous real multi-account UI population;
- final human normal-user built-executable walkthrough.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-scan-restart-safety.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7136.json` — full 47-case snapshot superseding the R7-134 case-status snapshot; only B08/D04 are promoted by R7-136 evidence.
