# R8-008 — restore original map_scan_clear strict parity

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** original Map Data Clear admission, storage scope, returned state, and frontend control boundary.

## Authority

The implementation authority is:
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`
- immutable recovered `MapDataPanel-C1HVeNHr.js` bytes under `evidence/lwbridge-0.3.1/frontend/assets`.

The recovered original public contract is `{serverId}`.

Admission is exact:
- active scan -> `SCAN_RUNNING / stop the map scan first`;
- requested `serverId` must be positive;
- it must equal the current scan-state `serverId`;
- current `serverIdSource` must be exact `live`;
- any failed server gate -> `SERVER_UNAVAILABLE / current server id unavailable`.

Only after those gates may Clear delete data.
## Original storage and UI behavior

Original storage Clear is server-scoped:
- delete `scan_runs` for the admitted live server;
- delete `map_records` for the admitted live server;
- preserve `player_marks`;
- refresh/publish an idle scan state.

The immutable frontend has exactly one `onClick:Qn` Clear binding.
That Clear is in Manual Scan and calls `map_scan_clear(L)`.
The original Auto Scan card does not contain a second Clear control.

Therefore the R7-147 rebuild behavior was a direct deviation:
- Auto exposed its own Clear button;
- `Qn()` was rewritten from `ne(L)` to `ne(0)`;
- backend/service accepted `serverId=0` as session-wide clear-all;
- saved/non-live server datasets could be deleted while the game was on another server.

## R8-008 implementation

`LWBridgeBackend` now calls `MapScanClearOwnership.Validate` before mutation and only calls `ClearServer(serverId)`.

`ManualMapScanCommandService` now derives the current scan-state server/source under the service lock, applies the same exact gate, and only clears the admitted live server.

The frontend generator no longer rewrites Clear to `serverId=0` and no longer injects the Auto Clear button.
The existing R7 saved-server browsing, one-shot Auto Scan, cross-server row navigation and Doom Walker Follow changes remain untouched for separate parity checkpoints.

## Regression coverage

Deterministic coverage now proves:
- `serverId=0` is rejected without mutation;
- a positive non-current server is rejected without mutation;
- no-live-ownership backend Clear fails closed;
- a fresh authoritative live server can be admitted;
- active-scan rejection retains exact `SCAN_RUNNING` precedence;
- valid Clear removes only current-live-server `map_records` / `scan_runs`;
- records/runs for other servers survive;
- player marks survive;
- repeated valid Clear remains safe/idempotent.

A dedicated generated-frontend check proves:
- Qn sends `L`, not `0`;
- the `serverId=0` rewrite is absent;
- there is exactly one `onClick:Qn` binding;
- the API still exposes `map_scan_clear`.

The broader R7-147 browser fixture was updated so its unrelated workflow coverage remains valid without asserting the removed clear-all deviation.
## Validation

Passed on 2026-09-24:
- `python tools/build_lwbridge_frontend.py`
- `python tools/build_lwbridge_frontend.py --check`
- `node tools/check_map_clear_r8008.cjs`
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --nologo`
  - 0 warnings
  - 0 errors
- full deterministic checks DLL
  - `ok=true`
  - `failures=[]`
- `node tools/check_map_owner_workflow_r7147.cjs`
- `node tools/check_lwbridge_frontend.cjs`
  - 36 browser comparisons passed
- City Excel browser regression
- Automatic scan strategy UI regression
- R7-131 Map Data persistence / Stop / Clear-race regression
- R7-156 Auto navigation / reconnect regression
- R7-136 Auto restart regression
- visual comparison: 32 pairs, 28 strict pairs, strict high-delta rate 0.

## Not claimed by R8-008

This checkpoint does not claim full Map parity.
Normal/Fast restoration, rebuild-only `zombie_boss`, `server_jump` result parity, `map_summary`, `map_data_options`, original Auto Scan, Scheduled Plunder, and protected acquisition internals remain separate work.
