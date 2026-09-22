# Current implementation handoff

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R7-150`; parent revision `e594455ff43abe38d657f66a4eec3eb243e3585c`
**Date:** 2026-09-23

Read `AGENTS.md` first. Preserve evidence-first recovery rules, the SB-79 restriction, unrelated diagnostic WIP, and commit/push verification requirements.

## Current product state

Home / Overview remains at its accepted evidence scope. Map Data received owner-workflow corrections in R7-147, direct Truck/Railway Train-list acquisition in R7-148, Scheduled Plunder retirement in R7-149, and covered cross-server no-jump Auto routing in R7-150; the R7-145 acceptance matrix still contains 47 cases with **zero ordinary `partial` rows**, and R7-147 does not promote any population/authorization gate.

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

On the standard 1000x1000 world, `MapScanStrategyPlanner` automatically selects the current proven strategy. Mixed scans use the exact Fast AOI path; Zombie Boss-only uses the dedicated LOD2 path; Truck/Railway-only uses the official Train list. R7-150 live-proved a covered remote Truck/Railway target in 0.809 s without server travel while keeping dataset `serverId` separate from physical `liveServerId`. Uncovered/mixed targets still use the existing jump-first path.

Further optimization is possible only if it preserves fresh authoritative identity/coverage. R7-150 deliberately refreshes the official Train list for each target instead of introducing an invented stale-snapshot cache lifetime.

## Remaining gates

- B03/B13/B14/C01: population-dependent Ghost/Supplies positive rows.
- E01/E02: Treasure consuming action remains unrouted; protected scope/lucky/scout scheduler semantics are still blocked behind SB-79.
- E03/E04/E05: retired by owner in R7-149; Scheduled Plunder is no longer a product or live-acceptance surface.
- E06: live Alliance-share delivery requires explicit messaging authorization.
- Simultaneous real multi-account UI population remains unavailable.
- Final integrated release acceptance remains separate from ordinary technical completion.

Ghost positive-row proof remains owner-deferred until 2026-09-24. Current Supplies rechecks on 2212 and 2213 produced zero authentic Supplies rows. R7-150 current-v20 Train-list coverage observed Railway population on server 2207. A fresh Railway row/Follow acceptance remains population-dependent on the chosen proof target.

## What not to do

Do not replay or reroute SB-79. Do not invent protected Treasure scheduler semantics. Do not perform a consuming claim/message action merely to turn an acceptance row green. Do not restore Scheduled Plunder unless the owner explicitly reverses the R7-149 retirement. Do not delete old evidence to make the repository look cleaner.

## Evidence navigation

Start with `evidence/lwbridge-implementation/README.md` and `evidence/lwbridge-implementation/2026-09-22-r7-current-evidence-index.json`. Historical evidence remains valid at its original source/build/scope and is retained for auditability.
