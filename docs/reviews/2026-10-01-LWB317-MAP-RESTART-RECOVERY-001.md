# LWB317-MAP-RESTART-RECOVERY-001 — restart/reopen and saved-state semantics

**Date:** 2026-10-01  
**Branch:** `research/offline-controller`  
**Starting HEAD:** `086757e36562b76d7e45b857a282c51267e16171`  
**State:** `AWAITING_REVIEW`

## Accepted predecessor

The project lead independently accepted `LWB317-MAP-UI-PRODUCTIONIZE-001` at
`086757e36562b76d7e45b857a282c51267e16171`. Its historical review/evidence is
unchanged. `src/LWBridge.UI-0.3.17` remains the canonical production frontend;
`src/LWBridge.Desktop/WebUi` remains recovery/reference only.

## Recovered restart contract

The recovered public `map_scan_start` contract contains an optional `resume`
input, but the exact 0.3.17 state construction/reset sites recovered so far all
write `resumeAvailable=false`; no exact true-producing constructor is known.
Accordingly the production rebuild continues to expose no public interrupted-run
resume behavior. `ManualMapScanCommandService` acquires the exclusive per-profile
`MapScanProcessLease`, marks any persisted orphaned `running` run `failed` with
`map scan interrupted by application restart`, preserves its durable block/staging
rows as interruption evidence, and starts a new run. A process that cannot acquire
the lease does not reconcile another process's active run.

The publication boundary remains transactional. Reconciliation does not copy
`scan_records` staging rows into `map_records`, and a reconciled failed run cannot
later publish. Previously published Map rows remain queryable after store reopen.
This is a fresh-run-after-interruption contract, not a resume contract.

## Recovered frontend persistence

Hash-locked 0.3.17 frontend evidence
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`
(SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`)
uses local storage for the Manual scan mode key `lwbridge.mapScanMode` and the
two Treasure preference keys `lwbridge.mapIncludeForeignRadarTreasures` and
`lwbridge.mapLuckyTreasurePriority`. The recovered Map panel does not persist
Manual selected categories, Manual/Auto tab, result tab, or browse-server state.

The canonical React Map page had omitted the exact recovered Manual scan-mode
persistence. This checkpoint restores only `lwbridge.mapScanMode`. It does not
carry forward the historical 0.3.1 rebuild's later per-profile persistence of
selected categories/result tab/browse server, because that behavior is not in
the recovered 0.3.17 package.

Saved Map datasets are a separate persistence concern: SQLite `map_records`
survive Desktop reopen and can be queried again. That does not imply that a
cross-process UI browse-server selection is remembered.

## Deterministic coverage

The existing restart regression
`ManualMapScanCommandServiceChecks.RestartReconcilesOrphanedRunAndRejectsConcurrentOwner`
exercises the required backend boundary: durable interrupted `running` row,
exclusive ownership, restart reconciliation, retained partial checkpoints,
stale staging rejection, retained prior published rows, rejected publication of
the failed orphan, and a subsequent fresh scan with `resumeAvailable=false`.

The canonical UI integration check now also pins the exact recovered
`lwbridge.mapScanMode` read/write key and rejects invented local-storage
persistence for selected types, browse server, result tab, and scan tab.

## Live scope

No new gameplay-affecting action is required by this contract. The existing
normal-launch production acceptance already establishes the canonical zero-argument
Desktop frontend and assistant-owned Map scan path. This checkpoint does not
relabel deterministic crash/reconciliation coverage as live proof; a dedicated
mid-scan Desktop-termination orchestration remains optional evidence rather than
a prerequisite for the recovered fresh-run policy.

## Classification

- interrupted persisted `running` row reconciliation: `IMPLEMENTED_NOT_VALIDATED`
  at live process-termination scope; deterministic coverage passes;
- exclusive scan ownership across processes: `IMPLEMENTED_NOT_VALIDATED` at live
  multi-process scope; deterministic file-lease coverage passes;
- fresh scan after reconciled interruption: `IMPLEMENTED_NOT_VALIDATED` at live
  restart scope; deterministic coverage passes;
- stale staging cannot publish: `EXACT_CONTRACT` publication boundary with
  deterministic coverage;
- published Map rows survive database reopen: `IMPLEMENTED_NOT_VALIDATED` at
  normal Desktop-reopen UI scope; deterministic file-backed coverage passes;
- recovered Manual scan-mode persistence: `EXACT_BYTES` / implemented in the
  canonical frontend;
- cross-process selected-category/result-tab/browse-server persistence:
  `OUT_OF_SCOPE` for strict 0.3.17 parity because the recovered 0.3.17 Map panel
  does not persist those controls;
- original private/protected traversal equivalence: `UNKNOWN`.

