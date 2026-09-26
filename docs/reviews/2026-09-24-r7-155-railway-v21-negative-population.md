# LWB-R7-155 — Railway v21 source proof / negative population

**Date:** 2026-09-24
**Branch:** `research/offline-controller`
**Parent revision:** `f7711e4d9e08a8dbbc1765dafc73a2aa35c62ebb`
**Evidence:** `evidence/lwbridge-implementation/2026-09-24-r7-155-railway-v21-negative-population.json`

## R7155-RAIL-01 — strict Railway full scan

After the externally owned game session closed, the unchanged strict `LiveManualFullRailwayProof` was rerun on current Last War content version 21 targeting server 2207.

The Railway-only scan completed **2,500/2,500** logical blocks with no scan failure/unread condition. It then failed only at the intentional positive-population guard:

`Ordinary Manual Railway scan published no Railway/Train records.`

Observed process runtime was about 34.17 s. Because no positive row existed, the harness did not claim v21 positive-row sort/reopen or Follow acceptance.

## R7155-RAIL-02 — current-v21 direct Train-list source

The official Train-list population harness then queried:

2175, 2180, 2182, 2185, 2190, 2195, 2196, 2204, 2207, 2212, 2213.

Every query completed in `state=proven` through the game-owned Railway source. Every server returned **0 Railway rows**. The harness returned to original server 2212 and cleanup left LastWar, LastWarLauncher, LWBridge.Desktop, and LWBridge.OverviewHelper all stopped.

This is stronger than the R7-154 status-only result: the Railway direct source is now freshly live-proven on v21. The remaining gap is positive population/Follow, not source acquisition.

## R7155-RAIL-03 — live-proof identity correction

`LiveTrainListPopulationProof` previously emitted the historical proof string:

`current_v20_official_train_list_population`

That label was misleading on v21, even though the runtime behavior was current-client. The test harness now emits:

`current_client_official_train_list_population`

No production behavior changed.

## Acceptance consequence

Historical v20 positive Railway acquisition/Follow remains valid provenance. Current-v21 direct Railway source acquisition is now live-proven negative-population across the sampled 11 servers. A fresh v21 **positive row + real Follow + reopen/sort** pass remains population-gated.

The 47-case acceptance matrix is unchanged.
