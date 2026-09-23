# LWB-R7-151 — v21 update-safe lifecycle and Secret Task acceleration

**Date:** 2026-09-24
**Branch:** `research/offline-controller`
**Evidence:** `evidence/lwbridge-implementation/2026-09-24-r7-151-v21-dispatch-fast-scan.json`

## R7151-UPD-01 — update-safe startup order

**Status:** IMPLEMENTED/OFFLINE-TESTED.

The startup owner now restores any LWBridge-modified files first, runs the untouched official launcher/update path, lets the official update finish, closes the clean preflight game, validates the final client strictly, and only then installs/starts LWBridge. Restore-only recovery may finalize restoration against an intermediate newly updated binary that is not yet approved; candidate injection still requires the normal strict compatibility validator on the final client.

**Primary locators:** `OverviewLifecycleService`, `OverviewLifecycleOfficialSettle`, `CurrentClientCompatibility`, `tools/run_overview_bridge.py`, and `tools/recover_overview_pending_current.py`.

**Validation:** deterministic desktop checks and `python tools/check_current_client_runtime_contract.py` pass against Last War content version 21.

## R7151-MAP-01 — safe v21 wide AOI geometry

**Status:** LIVE-PROVEN.

`CurrentClientMapBlockSource.FastCity.cs` uses `FastWideCoverageCameraY = 220` and aligned AOI-boundary targets. Live geometry sweep at FOV 175 showed aspect 4/6/8/10/12 all produce exactly 6x25 = 150 AOIs. CameraY 240 and above enters the v21 split path and is rejected. Lower heights tile the map in more requests.

The production primary grid is 17 horizontal targets x 4 vertical targets = 68 requests. Exact returned AOI indices remain the coverage truth; cleanup still requires the full 10,000-cell union.
## R7151-MAP-02 — settle timing and live scan speed

**Status:** LIVE-PROVEN.

The one-time camera parking settle was tested at 500 ms and 1 s. 500 ms failed the first bounded/stable request; 1 s passed. A separate startup-settle delay was then removed after live zero-startup-settle proofs completed 2,500/2,500 logical blocks.

Observed current-client results:
- Dispatch server 2175: 7.028 s, 2 rows published and reopened, all maintained sort/filter checks passed.
- City server 2175: 10.393 s, 94 rows published and reopened.
- Same-session multi-server Dispatch before removing the redundant startup delay: 2175 9.92 s, 2180 6.28 s, 2185 6.31 s; later jumps were about 0.13 s.

These are observations, not fixed SLA promises.

## R7151-MAP-03 — official Secret Task Quick Find

**Status:** LIVE-PROVEN.

The current v21 client exposes `MsgDefines.DispatchFindNearestPoint` with response handler `Net.Msgs.DispatchTask.DispatchFindNearestPointMessage`. The probe implementation is `tools/current_live_resource_probe.lua`, `dispatch_nearest_runtime` (transport at lines ~7133/7183-7301 in this checkpoint). `LiveDispatchNearestProof` proved a read-only lookup in roughly 0.5 s with no explicit server jump or gameplay action.

Repeated calls returned the same point, so this is a one-target finder, not a world-wide enumeration API. It must not replace the complete scan.

## R7151-MAP-04 — Auto Scan immediate result

**Status:** IMPLEMENTATION POLICY + BROWSER-PROVEN.

When Auto Scan includes Dispatch and has confirmed travel to a target server, the frontend now issues one native Quick Find before `map_scan_start`, emits `lwbridge-map-auto-dispatch-quick-find`, and shows the coordinate in the Auto card. The result is informational only and is never merged into authoritative scan storage; the complete 68-request scan still runs.

`tools/check_map_data_r7131.cjs` verifies the Quick Find command and visible-result event occur before `map_scan_start`, while existing Stop, Clear-race, saved-server and persistence behavior stays intact.
## Rejected experiments

The following were investigated and intentionally not promoted:
- explicit `messagebulk` world browsing: v21 completion could not be proven safely for empty batches;
- in-game coverage chaining: not production-proven and removed from routing;
- aspect-only tuning: no footprint improvement over 6x25 at cameraY 220.

## Reproduction

- `dotnet tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll --live-current-client-full-dispatch-manual`
- `dotnet tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll --live-current-coverage-geometry-sweep`
- `python tools/check_current_client_runtime_contract.py`
- `node tools/check_map_data_r7131.cjs` with the existing cached Playwright module

## Validation and limits

Deterministic desktop checks: pass, zero failures. v21 runtime contract: pass. Map browser regression: pass. Packaged live-resource probe: 63,971 bytes, below the 64 KiB require-able entry limit. `git diff --check` reports only line-ending warnings.

The normal Release `.ps1` window verifier could not be rerun because this machine blocks PowerShell script execution with `PSSecurityException`; no execution-policy change was attempted. `Start LWBridge.cmd --self-test` passed.

## Implementation impact

R7-151 keeps the normal backend strategy ownership and exact-coverage/publication contracts. Secret Task complete scans are now much faster on v21, and Auto Scan can surface the first native task coordinate immediately without weakening completeness. Further speed gains require a different authoritative server list/query; the safe camera geometry is at its currently proven floor.
