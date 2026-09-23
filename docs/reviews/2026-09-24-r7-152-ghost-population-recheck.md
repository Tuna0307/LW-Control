# LWB-R7-152 — Ghost Ops population recheck

**Date:** 2026-09-24
**Branch:** `research/offline-controller`
**Parent revision:** `b7daab82880b26148aea5771e3b756fa451a43b9`
**Evidence:** `evidence/lwbridge-implementation/2026-09-24-r7-152-ghost-population-recheck.json`

## R7152-GHOST-01

**Status:** LIVE-PROVEN negative population; positive-row acceptance remains population-gated.

The existing strict `LiveManualFullGhostProof` was rerun on the first owner-eligible date against Last War content version 21. The current live server 2212 and sampled servers 2175, 2180, 2185, and 2207 each completed the Ghost-only full-world scan at 2,500/2,500 logical blocks with no scan failure/unread condition. Every run then failed only at the intentional positive-population guard:

`Ordinary Manual Ghost scan published no Ghost Ops records.`

No production Ghost parser/filter/sort contract was weakened and no synthetic row was used.
## Reproduction and implementation impact

The proof now accepts `LWBRIDGE_MANUAL_SCAN_SERVER`, mirroring the existing Dispatch proof. A valid target is entered with the public `server_jump` command inside the owned lifecycle before `map_scan_start`. Lifecycle stop remains responsible for cleanup even when the zero-row assertion throws.

Reproduction:

`set LWBRIDGE_MANUAL_SCAN_MODE=fast && set LWBRIDGE_MANUAL_SCAN_SERVER=<server> && dotnet tests\LWBridge.Desktop.Checks\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.Checks.dll --live-current-client-full-ghost-manual`

Observed process runtimes were approximately 40.75 s (2212), 39.09 s (2175), 39.76 s (2180), 40.01 s (2185), and 38.70 s (2207). These include lifecycle startup/cleanup and are not scan SLAs.

After the final 2207 attempt, LastWar, LastWarLauncher, LWBridge.Desktop, and LWBridge.OverviewHelper process counts were all zero. The latest jump evidence recorded 2212 -> 2207 under profile `manual-full-ghost-proof`.

## Acceptance consequence

B03/B13/B14/C01 Ghost positive-row acceptance is **not promoted**. The scanner and publication path proved clean completion; the missing prerequisite is authentic Ghost Ops population on the sampled servers at the sampled time.

A later recheck should reuse this unchanged strict proof. A positive result must still preserve the required `GhostreconPointInfo` / `TableName.LwGhostreconTask` source fields and pass the existing Ghost sort/reopen assertions.
