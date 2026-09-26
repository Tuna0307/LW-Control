# LWB-R7-153 — Supplies population recheck

**Date:** 2026-09-24
**Branch:** `research/offline-controller`
**Parent revision:** `ee42daccb0c25eab7e02d5ecc7f12a553dc5b80a`
**Evidence:** `evidence/lwbridge-implementation/2026-09-24-r7-153-supplies-population-recheck.json`

## R7153-SUPPLIES-01

**Status:** LIVE-PROVEN negative population; positive-row acceptance remains population-gated.

The existing strict `LiveTreasureStateRefreshProof` was rerun unchanged against Last War content version 21 with `LWBRIDGE_REQUIRE_SUPPLIES=1`. Servers 2212, 2175, 2180, 2185, 2207, and 2213 each completed the Treasure-family full-world scan at 2,500/2,500 logical blocks with no scan failure/unread condition.

No authentic `WorldSuppliesPoint` row was published on any sampled server. Server 2212 contained one ordinary Treasure row and zero Supplies; the other five sampled servers had zero Treasure-family rows at the sampled time.
## Proof contract and cleanup

The harness already accepts `LWBRIDGE_SUPPLIES_TARGET_SERVER`. For a remote target it verifies the public `server_jump` transition, confirms authoritative live context on the requested server, runs the automatic Fast full-world Treasure scan, requires positive Supplies population before state-refresh assertions can pass, and returns to the original server during cleanup.

The final 2213 attempt recorded a proven `2213 -> 2212` back-self-server transition. After cleanup, LastWar, LastWarLauncher, LWBridge.Desktop, and LWBridge.OverviewHelper process counts were all zero.

Observed process runtimes were approximately 38.22 s (2212), 37.84 s (2175), 37.99 s (2180), 37.87 s (2185), 38.69 s (2207), and 38.92 s (2213). These include lifecycle startup/cleanup and any cross-server travel, so they are not scan SLAs.

## Acceptance consequence

B13/B14/C01 Supplies positive-row acceptance is **not promoted**. This checkpoint strengthens the explanation for the population gate on the current v21 client; it does not indicate a production parser, query, or state-refresh defect.

A later recheck should reuse the unchanged strict proof. A positive result must still identify `WorldSuppliesPoint`, preserve `TableName.LWIceSupplies` source identity, and pass the existing Supplies state-refresh field assertions.
