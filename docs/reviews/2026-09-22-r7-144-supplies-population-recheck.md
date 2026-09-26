# LWB-R7-144 — Current Supplies population recheck

**Date:** 2026-09-22
**Base revision:** `65355677cccc8f4a64c8847923d073ff686c0a97`
**Status:** POPULATION_PENDING / CURRENT_RECHECK_NEGATIVE

## Goal

Refresh the positive-population gate for Supplies without changing production behavior or weakening acceptance.

R7-072 remains the prior exhaustive reachable-band authority from 2026-09-20. R7-144 does not repeat that broad sweep. It rechecks the two immediately relevant current servers, 2212 and 2213, through the unchanged strict read-only acceptance harness.

## Acceptance boundary

Harness:

`tests/LWBridge.Desktop.Checks/LiveTreasureStateRefreshProof.cs`

Strict gate:

`LWBRIDGE_REQUIRE_SUPPLIES=1`

A positive pass requires at least one authentic published Supplies row with point type 27, positive `suppliesType`, zero `treasureType`, and runtime class ending in `WorldSuppliesPoint`.

## Current live recheck — server 2212

The existing owned-session harness completed its full Fast Treasure scan:

- 2,500 / 2,500 blocks completed;
- 0 failed blocks and 0 unread blocks, as required before the strict population gate;
- 7 ordinary Treasure rows published;
- 0 Supplies rows published.

The strict positive gate therefore failed exactly as designed with:

`Supplies proof requires a positive live Supplies row; published ordinary=7, supplies=0.`

No acceptance status is promoted by a negative population observation.

## Current live recheck — server 2213

The same unchanged harness then targeted server 2213 and completed:

- 2,500 / 2,500 blocks;
- 0 failed blocks and 0 unread blocks before the population gate;
- 2 ordinary Treasure rows published;
- 0 Supplies rows published.

The strict gate again failed exactly as designed. The harness completed its return and cleanup path without a reported return error.

## Safety and cleanup

Both runs were read-only population/state probes and performed no state-changing gameplay or messaging action.

After the runs:

- no LWBridge Desktop, LastWar, launcher, or Overview helper process remained;
- installed package version remained 20;
- `LWScripts.data` SHA-256 remained `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`;
- `LWScripts.txt` SHA-256 remained `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`;
- `version.txt` SHA-256 remained `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`.

## Acceptance effect

Supplies remains **READY_FOR_POSITIVE_POPULATION** but **positive live acceptance remains OPEN**.

B13, B14, and C01 remain `partial_population`; R7-144 only refreshes the evidence explaining why. B03 is unchanged because its remaining population gate is Ghost, which remains owner-deferred until 2026-09-24.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-supplies-population-recheck.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7144.json`
