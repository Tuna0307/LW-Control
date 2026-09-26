# LWB-R7-154 — Railway v21 status hygiene

**Date:** 2026-09-24
**Branch:** `research/offline-controller`
**Parent revision:** `e88a7f160a7a2ff856a30ddead8e04766e4e773e`
**Evidence:** `evidence/lwbridge-implementation/2026-09-24-r7-154-railway-v21-status-hygiene.json`

## R7154-RAIL-01 — fresh Railway proof remains pending

A fresh current-v21 Railway positive-row/Follow acceptance was attempted with the existing strict `LiveManualFullRailwayProof`, targeting server 2207 via `LWBRIDGE_RAILWAY_TARGET_SERVER=2207` in Fast mode.

The proof did not start. `OverviewLifecycleService` rejected admission with:

`Close the game started outside this application first.`

Inspection showed only an externally launched `LastWar.exe` process. No launcher, LWBridge desktop, or LWBridge helper process was running. The external game was deliberately left untouched: no close, attach, injection, or ownership takeover was attempted.
## R7154-RAIL-02 — current wording corrected

Current status pages previously used phrases such as “current-v20 Railway,” which became misleading after the client moved to content version 21. The corrected status is:

- production still uses the official `LWTrainDataManager.TryGetTrainList(true)` Railway/Truck source;
- historical v20 positive Railway acquisition/Follow evidence remains valid provenance;
- a fresh positive v21 Railway row/Follow proof is still required and has not been promoted.

This checkpoint does not downgrade historical evidence or claim a v21 source failure. It only prevents v20 live proof from being misread as a fresh v21 pass.

## R7154-SCOPE-01 — retired Plunder guidance corrected

Two current-guidance references still listed Truck/Dispatch state-changing Plunder acceptance as pending. Those were stale. Scheduled Truck/Dispatch Plunder was owner-retired end-to-end in R7-149.

Current authorization-gated state-changing work is limited to Treasure and Alliance surfaces. The 47-case acceptance matrix is unchanged.

## Next valid Railway proof

Reuse the unchanged strict harness when LWBridge can legitimately own the game session:

`set LWBRIDGE_MANUAL_SCAN_MODE=fast && set LWBRIDGE_RAILWAY_TARGET_SERVER=2207 && dotnet tests\LWBridge.Desktop.Checks\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.Checks.dll --live-current-client-full-railway-manual`

A passing result still requires authentic Railway rows, persisted/reopened identity/fields, recovered sort checks, real Follow, and safe return to the original server.
