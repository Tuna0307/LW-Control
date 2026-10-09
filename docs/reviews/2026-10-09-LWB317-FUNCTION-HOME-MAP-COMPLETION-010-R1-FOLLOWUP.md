# Completion-010 R1 continued implementation — PARTIAL (2026-10-09)

Starting repository checkpoint: `9f8291d33a5d6cbb1a8e58a81e3fc36ebc768709`, clean `research/offline-controller`.
This is a subsequent solo continuation, NOT a rewrite of the archived R1 report, lead review, original obligation inventory, or 0.3.17 evidence.

## Additional product corrections and distinguishing negatives

### MAP-010R1-F01 — loose optional Map search markedOnly

Original 0.3.17 Map handler recovery `COMPLETION-010/d-map-decode/map-handlers-recovery.md`, search coercion section (native 0x3EB60E/0x3E1211): typed-wrong optional filters do not reject a search.
Actual `Map317CommandService.NormalizeOriginalSearch` passed a malformed `markedOnly` through strict `MapDataQueryContract.OptionalBoolean`, throwing `INVALID_MAP_QUERY`.
An actual command/SQLite regression using non-boolean `"true"`, `1`, `[true]`, and `null` first failed: `markedOnly must be a boolean`, stack from `MapDataQueryContract.cs:355`. Production now drops malformed optional `markedOnly` exactly as it already does for the other native optional booleans. Inert replay after the fix: `--map317-native-boundary-check` exit 0.

### MAP-010R1-F02 — Auto Scan stale due admission after disable

The production `MapAutoScanCommandService.TryAdmitDueCycleAsync` read due/enabled under `stateGate`, released it, then published a new active cycle under `cycleGate` without checking whether Disable had since committed. A test-controlled await in that gap reproduced a ghost admitted cycle after the persisted disabled configuration had `nextRunAt=0`: `--map-auto-scan-campaign-check` failed with `old enabled/due observation must not launch even a ghost cycle after disable commits`.
The final admission now reloads persisted config while holding `stateGate`, and publishes active owner under nested `cycleGate` in the existing stateGate->cycleGate lock order. No cycle can be newly admitted using a config already invalidated before that reservation. The test seam is inert and not active in the production scheduler.
After correction: `--map-auto-scan-campaign-check` exit 0. This is a source-compatible controlled admission/ownership correction, **not** proof of the entire protected original Auto producer lifetime.

## Verification / safety

- Regression first-red / after-green `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release -- --map317-native-boundary-check`: final exit 0, `{"ok":true,"check":"map317-native-boundary"}`.
- First-red / after-green `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release -- --map-auto-scan-campaign-check`: final exit 0, `{"ok":true,"check":"map-auto-scan-campaign"}`.
- `dotnet run --no-build --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release`: exit 0, deterministic flow `ok:true`, `failures:[]`, observed `gameRunning:false` and `launcherRunning:false`.
- `git diff --check`: no whitespace errors.
- Entirely static/inert; **0 new game launches**, no desktop input/screenshots, no protected original-service interactions, no live claims.
- Prior two real bounded live pilots, restored hashes/roots, original 133 obligations and negative native UI crash are unchanged.

## Remaining work — PARTIAL

Home protected launch reservation/27 launch failures/finalizer callback, actual multi-owner runtime, current-client repair/restart/retained reconnect, Map original scheduler/Truck/day-history/Auto lifetime deep parity, per-kind protected Lua acquisition/claim correlation, and genuine EN/light + JA/dark packaged UI remain unresolved. No original A-to-A or project-lead acceptance upgrade. Use `R1/obligations-r1-derivative.json`, the current Home/Map masters, and the R1 worker report as the next continuation base.
