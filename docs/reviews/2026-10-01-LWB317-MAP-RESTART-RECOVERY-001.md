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
resume behavior.

The first checkpoint of this stage found that the older
`ManualMapScanCommandService` already had a restart lease/reconciliation policy,
but the normal canonical Desktop path is `Map317CommandService` ->
`MapControlPlane`. That production path initially opened the durable per-profile
Map317 database with a fresh in-memory idle state and had no cross-process scan
lease/reconciliation. This was a real production integration gap relative to the
already-established R7 restart-safety implementation policy, so that policy has
now been applied at the Map317 composition boundary: startup reconciles
orphaned `running` rows only while holding the profile database scan-owner lease;
Start owns that lease for the accepted scan lifetime; a second process gets the
recovered `SCAN_RUNNING / map scan already running`; and a terminated provider run
releases ownership. Reconciliation marks the orphan `failed` with
`map scan interrupted by application restart` without publishing its staging rows.
That interruption string and stale-staging rule are rebuild implementation policy
from R7-136; they are not claimed as exact 0.3.17 bytes. The exact 0.3.17 ordinary
failure transaction is different and may preserve captured rows.

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

The older restart regression
`ManualMapScanCommandServiceChecks.RestartReconcilesOrphanedRunAndRejectsConcurrentOwner`
remains compatibility coverage. New Map317 store coverage pins canonical
reconciliation of durable `running` rows to `failed`, the implementation-policy
interruption reason, stale-staging non-publication and rejected completion of the reconciled
run. `Map317RestartChecks` pins exclusive file ownership and reacquisition after
owner release for the canonical database path.

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
  at live process-termination scope on the canonical Map317 path; deterministic
  coverage passes;
- exclusive scan ownership across processes: `IMPLEMENTED_NOT_VALIDATED` at live
  multi-process scope; deterministic file-lease coverage passes;
- fresh scan after reconciled interruption: `IMPLEMENTED_NOT_VALIDATED` at live
  restart scope; deterministic coverage passes;
- stale staging cannot publish during restart reconciliation: implementation
  policy with deterministic coverage; this is not the exact 0.3.17 ordinary
  failure transaction;
- published Map rows survive database reopen: `IMPLEMENTED_NOT_VALIDATED` at
  normal Desktop-reopen UI scope; deterministic file-backed coverage passes;
- recovered Manual scan-mode persistence: `EXACT_BYTES` / implemented in the
  canonical frontend;
- cross-process selected-category/result-tab/browse-server persistence:
  `OUT_OF_SCOPE` for strict 0.3.17 parity because the recovered 0.3.17 Map panel
  does not persist those controls;
- original private/protected traversal equivalence: `UNKNOWN`.
