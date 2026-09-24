# Current implementation handoff

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R7-154`; parent revision `e88a7f160a7a2ff856a30ddead8e04766e4e773e`
**Date:** 2026-09-24

Read `AGENTS.md` first. Preserve evidence-first recovery rules, the SB-79 restriction, unrelated diagnostic WIP, and commit/push verification requirements.

## Current product state

Home / Overview remains at its accepted evidence scope. R7-151 adds the Last War v21 update-safe lifecycle and Secret Task acceleration. R7-152/R7-153 refresh Ghost/Supplies population evidence without promoting either gate. R7-154 clarifies that Railway positive acquisition/Follow is historically live-proven on v20 but still needs a fresh v21 positive row; the strict 2207 attempt correctly refused to take ownership while Last War was externally launched. The 47-case acceptance matrix remains unchanged with **zero ordinary `partial` rows**.

Use these current summaries instead of reconstructing status from chronological checkpoint prose:

- `docs/tabs/home.md`
- `docs/tabs/map-data.md`
- `docs/tabs/shared-release.md`
- `docs/lwbridge-project-status.md`
- `docs/external-audit-guide.md`

Current machine-readable acceptance source:

`evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`

## Home / Overview

All A01-A12 ordinary acceptance cases are closed at their documented scopes. Current evidence covers lifecycle, process ownership, reconnect, fault handling, startup rollback, authenticated Refresh Status, cross-server navigation, normal Release responsiveness, and zero-argument normal-user Overview/Map navigation across restart.

No ordinary Home defect is currently open. Simultaneous real multi-account UI population remains an availability-only integrated test gap.

## Map Data

The shared Manual Scan engine, Auto Scan scheduler, transactional SQLite publication, filtering/sorting/paging, marks, navigation, restart safety, and native point/march transitions remain accepted at their current evidence scopes. R7-147 corrected eight owner-observed workflow defects: session-scoped scan data, Auto **All**, one-shot Run Now while recurring Auto is off, cross-server row navigation, stopped/session-wide Clear, Doom Walker Follow, current-v20 official Train-list acquisition, and removal of the misleading Manual server filter.

On the standard 1000x1000 world, `MapScanStrategyPlanner` automatically selects the current proven strategy. Truck/Railway-only still uses the official Train list. R7-151 current-v21 Dispatch uses the aligned 6x25 AOI footprint at cameraY 220, requiring 68 primary requests for the exact 10,000-AOI union; a live server-2175 full scan completed in 7.028 s with rows persisted/reopened. The native `DispatchFindNearestPoint` call returns one task in about 0.5 s; Auto Scan shows that result after confirmed travel and before starting the complete scan, but never treats it as complete coverage.

The failed message-bulk browser and in-game coverage-chain experiments were removed. CameraY 240+ enters the v21 split path, so further complete-scan speed gains require a different authoritative server query/list rather than weaker coverage.

## Remaining gates

- B03/B13/B14/C01: population-dependent Ghost/Supplies positive rows.
- E01/E02: Treasure consuming action remains unrouted; protected scope/lucky/scout scheduler semantics are still blocked behind SB-79.
- E03/E04/E05: retired by owner in R7-149; Scheduled Plunder is no longer a product or live-acceptance surface.
- E06: live Alliance-share delivery requires explicit messaging authorization.
- Simultaneous real multi-account UI population remains unavailable.
- Final integrated release acceptance remains separate from ordinary technical completion.

R7-152 performed the first eligible current-v21 Ghost positive-population recheck: strict 2,500/2,500 scans completed on 2212, 2175, 2180, 2185, and 2207, but all five produced zero authentic Ghost rows. R7-153 then refreshed Supplies on current v21 across 2212, 2175, 2180, 2185, 2207, and 2213; all six clean complete scans produced zero authentic `WorldSuppliesPoint` rows. Both remain population-gated. A fresh Railway row/Follow acceptance remains population-dependent on the chosen proof target.

## What not to do

Do not replay or reroute SB-79. Do not invent protected Treasure scheduler semantics. Do not perform a consuming claim/message action merely to turn an acceptance row green. Do not restore Scheduled Plunder unless the owner explicitly reverses the R7-149 retirement. Do not delete old evidence to make the repository look cleaner.

## Evidence navigation

Start with `evidence/lwbridge-implementation/2026-09-24-r7-154-railway-v21-status-hygiene.json`, `docs/reviews/2026-09-24-r7-154-railway-v21-status-hygiene.md`, then R7-153 Supplies, R7-152 Ghost, and R7-151 v21 lifecycle/Dispatch evidence. Historical evidence remains valid at its original source/build/scope and is retained for auditability.
