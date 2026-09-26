# LWB-R7-131 — Map Data browser acceptance and Clear/search race closure

**Date:** 2026-09-22
**Scope:** close the R7-130 browser-only follow-up for persisted Map Data controls, multi-server saved browsing, Auto Stop sequencing, and the owner-reported Clear/Search false-error race.
**Overall status:** **IMPLEMENTED/OFFLINE-TESTED** against the generated shipped frontend in real headless Microsoft Edge. No official-game state-changing action was performed, and this checkpoint does not replace the still-open final normal-user built-executable walkthrough.

## Source identity and toolchain

The implementation was based on pushed R7-130 revision `6aa0b240b2fb340b3716b39409202465222d5672` on `research/offline-controller`.

Relevant source identities after the R7-131 fix:
- `tools/build_lwbridge_frontend.py` SHA-256 `52DA93262E8054ACDCB368623807C37972F2439ABB236315FCE482B6BAF33C23`
- generated `src/LWBridge.Desktop/WebUi/assets/MapDataPanel-C1HVeNHr.js` SHA-256 `91757A4E344EA31076BFA6FF4ADFD0B80D39E70C843B112A55C4E61CE60B3334`
- `tools/check_map_data_r7131.cjs` SHA-256 `4EE1976B131D67039ECAAFF6450A44A8D6EA6E11E49876D2BC1DF85845364520`

Local browser acceptance used:
- Playwright `1.63.0`, installed only under `C:\Users\chimw\AppData\Local\Temp\lwbridge-playwright`
- Microsoft Edge `153.0.4234.32`
- repository CI remains pinned to Playwright `1.62.1`; R7-131 adds the new check to the same existing browser-verification step.

## Finding R7-131-F1 — Clear invalidated the old request but immediately launched a replacement search

**IMPLEMENTED/OFFLINE-TESTED.**

R7-130 already incremented the saved-search and Treasure-refresh generations before `map_scan_clear`, so a request that began before Clear could no longer update UI state. Browser timing exposed a second path: Clear's own state resets changed dependencies of the automatic result-query effect, causing a new `map_search` one millisecond after `map_scan_clear`. If that replacement request overlapped the same SQLite transition, it could still surface the owner-visible message:

`Saved map search failed. Try Search again. Technical details remain in the application log.`

The canonical generator now adds one `clearAutoSearchOnce` ref owned by the Map Data panel. After a successful `map_scan_clear`, the next automatic search-effect turn is consumed without querying. The user-visible result remains the intentionally cleared empty set. Later genuine filter/tab/server changes and explicit Search continue through the ordinary query path.

Exact implementation locators:
- `tools/build_lwbridge_frontend.py`, R7-131 block immediately after the R7-130 saved-server selector patch.
- generated Map Data panel `clearAutoSearchOnce` ref, automatic query effect, and `Qn()` Clear handler.
- permanent browser regression `tools/check_map_data_r7131.cjs`.

Failure-first browser evidence before the fix showed:
1. `map_search(kind=city, keyword=stale-race)`
2. `map_scan_clear(serverId=2212)`
3. new `map_search(kind=city, keyword=stale-race)`
4. delayed rejection rendered the false saved-search error.

After the canonical fix, the same delayed-failure scenario records `map_scan_clear` followed by `map_data_options` only; there is no post-Clear `map_search` and no alert.

## Finding R7-131-F2 — R7-130 persistence and saved-server browsing work through the shipped UI

**IMPLEMENTED/OFFLINE-TESTED.**

The browser fixture uses the backend's real public shape, including `map_summary.savedServerIds=[2212,2213]` and `map_data_options.rewardItems={truck:[],railway:[]}`. It drives the generated Map Data bundle rather than a standalone recreation.

The check proves:
- seeded per-profile Manual categories restore as Player City + Monster;
- Auto Scan tab restores;
- Monster result tab restores;
- saved browse server 2213 restores while the synthetic live server remains 2212;
- the server selector contains both 2212 and 2213;
- after UI changes, Manual categories persist as Monster + Truck, result tab as Truck, browse server as 2212, and Auto tab as Auto;
- those four values survive a full document reload.

This closes the R7-130 browser-only persistence/server-selector follow-up at generated-frontend scope.

## Finding R7-131-F3 — Auto Stop prevents the next configured target

**IMPLEMENTED/OFFLINE-TESTED.**

The browser fixture configures targets 2212 and 2213, starts an Auto cycle, waits until the first `map_scan_start`, then clicks the shipped Stop button.

Acceptance requires:
- exactly one `map_scan_stop`;
- Auto Scan persisted with `enabled=false`;
- exactly one `map_scan_start`;
- no `server_jump` to 2213 after Stop.

The test passes. This directly exercises the R7-130 UI-to-parent scheduler handoff: the Stop button synchronously disables the scheduler before awaiting backend cancellation, so the top-level loop's per-target `Je.current.enabled` check prevents later targets.

## Reproduction and validation

Local browser commands:

```powershell
$env:NODE_PATH='C:\Users\chimw\AppData\Local\Temp\lwbridge-playwright\node_modules'
node tools\check_scan_strategy_auto.cjs
node tools\check_map_data_r7131.cjs
$env:LWBRIDGE_INTERACTIONS_ONLY='1'
node tools\check_lwbridge_frontend.cjs
```

All passed.

Canonical generation/syntax:
- `python tools\build_lwbridge_frontend.py --check` — pass.
- `node --check tools\check_map_data_r7131.cjs` — pass.
- `node --check src\LWBridge.Desktop\WebUi\assets\MapDataPanel-C1HVeNHr.js` — pass.

Release/deterministic:
- `dotnet build tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -c Release --no-restore` — **0 warnings / 0 errors**.
- `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -c Release --no-build` — all six deterministic groups true and `failures=[]`.

Full frontend/visual gate:
- `node tools\check_lwbridge_frontend.cjs` — **36 browser checks passed**.
- `node tools\check_city_export_removed.cjs` — pass.
- `python tools\compare_lwbridge_frontend.py` — 32 compared pairs; 28 strict pairs pixel-identical (`strictMaxMae=0`, `strictMaxHighDeltaRate=0`); four intentional Map Data pairs remained within the existing bounded override deltas.

CI integration:
- `.github/workflows/csharp.yml` now runs `node tools/check_map_data_r7131.cjs` in the existing Playwright browser-verification job.

## Validation limits and remaining work

This checkpoint is browser/offline proof of the shipped generated UI, not a claim that a human clicked the normal WinForms/WebView build. The final normal-user built-executable restart/walkthrough therefore remains open.

No Ghost or Supplies population appeared or was sought here; their positive-row live gates remain open. No Truck robbery, Dispatch plunder, Alliance share, Treasure claim/scout, message, spend, or other state-changing gameplay action ran. Those action acceptance gates remain subject to their existing recovered contracts and explicit suitable-target requirements.

R7-131 supersedes only the R7-130 statement that persistence/server browsing/Clear timing lacked browser interaction proof. R7-130's live scan/runtime evidence remains the current live authority.
