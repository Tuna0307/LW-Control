# LWB317-MAP-UI-PRODUCTIONIZE-001 — reconstructed UI becomes production default

**Date:** 2026-10-01
**Branch:** `research/offline-controller`
**Starting HEAD:** `13b25f6df2deb63206a24b50119666cf81f2b1ff`
**State:** `AWAITING_REVIEW`
**Scope:** frontend ownership/packaging/default selection plus one bounded normal-launch Map Resource acceptance. No server-jump, restart/resume, or additional Map-category campaign was started.

## Result

`src/LWBridge.UI-0.3.17` is now the canonical production frontend. An ordinary
`LWBridge.Desktop.exe` launch selects a packaged `ProductionUi` directory built
from that project. No `--map-ui-integration-proof` or `--ui-root` argument is
required. The live acceptance launched the Release executable with an empty
application-argument list and loaded `https://lwbridge.local/` with
`data-ui-project="LWBridge.UI-0.3.17"`, the packaged build identity below and
`data-bridge-mode="native"`.

`src/LWBridge.Desktop/WebUi` remains source-controlled and is still packaged as
`WebUi` for history, comparison and deliberate recovery. It is no longer the
normal selection. `--legacy-ui` is the explicit local recovery switch. Missing
or invalid canonical production assets fail clearly; the host does not silently
fall back to the legacy frontend.

## Production build/package architecture

`src/LWBridge.UI-0.3.17/scripts/build-production.mjs` performs the Vite build,
empties the selected output directory, emits the Vite manifest and writes
`lwbridge-ui-build.json`. The identity contains both a source fingerprint over
the canonical frontend inputs and an artifact fingerprint over the generated
package. `check-production-build.mjs` recomputes both fingerprints and rejects
missing, stale or changed output.

`LWBridge.Desktop.csproj` runs `npm ci`, builds the clean UI into its Release
intermediate directory, verifies it, clears `$(OutDir)ProductionUi`, and copies
the newly verified package there. Publish performs the same known-root copy.
The build therefore requires no manual asset copying and cannot reuse an old
`ProductionUi` tree silently. CI also runs the clean UI check/build/package
check and asserts that both the canonical package and the preserved legacy
recovery package are present.

The packaged build used by the live acceptance reported:

- source fingerprint `6cfb043426572bbe656f786e1e5656d8f887e2c38e89255aca35119a5547dea9`;
- artifact fingerprint `655e7e256a8add3f0d6c6bc9222d28e4d49d6d54138b159bb24ed2dac081bd72`;
- production `index.html` SHA-256 `b309f346f596c359ea52e221aa54efdea2a6e9978d558561897b7bf216e51edf`;
- legacy `WebUi/index.html` SHA-256 `abb44a00c76d59cbb3e090aa4d53b98350ab09ce977cd7afadaf60fb928d0135`.

Structured packaging evidence is
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/production-ui-package.json`.

## Host selection and security boundary

`DesktopUiContentRoot` owns normal frontend selection. The normal path requires
`ProductionUi/index.html` plus a valid `LWBridge.UI-0.3.17` build identity.
`--ui-root` remains admitted only when `--map-ui-integration-proof` explicitly
opens the existing proof override. `--legacy-ui` cannot be combined with the UI
proof paths. Ordinary backend/Map errors do not select another frontend.

The existing privileged origin is unchanged: Desktop still maps only the known
UI root to `https://lwbridge.local` through WebView2 and retains the existing
navigation/resource-origin restrictions. Bootstrap injection is unchanged.
The clean `backendBridge.js` continues the existing session/request protocol for
invoke, response, listen, unlisten, cancellation and structured errors over
`window.chrome.webview`; no second IPC protocol was introduced.

Browser/Vite preview remains explicitly non-native. The deterministic clean-UI
check proves preview mode has `available=false`, reports `mode="preview"`, and
rejects native commands with `PREVIEW_NO_NATIVE_HOST`; live mode without the
WebView transport reports `native-unavailable`.

## Normal-launch live acceptance

The primary acceptance was the actual Release executable with **zero application
arguments**. WebView2 remote debugging was supplied only as a process environment
diagnostic so the harness could observe and drive the rendered application; it
did not choose a UI root or proof mode. The harness created one assistant-owned
Last War instance through the already-existing native lifecycle command because
the reconstructed Home page intentionally remains a static reconstruction. All
Map operations were then driven through the normal reconstructed Map UI.

The launch proved `mode="live"`, a non-empty native session ID,
`window.chrome.webview`, clean UI project identity and native Map bridge mode.
All primary reconstructed pages rendered and activated without fatal runtime
errors: Home, Automation, Map Data, Squads / AFK, City Layout, Hotkeys,
Mini Games and Settings. Advanced remains conditional/dormant according to the
recovered 0.3.17 behavior.

The Resource acceptance selected only Resource Point and completed a normal
scan on server `2212`: 2,500/2,500 blocks read, zero failed, zero unread, zero
native dropped/pending records and 100% completion. This run observed **7,994**
Resource rows; the number is recorded only as this acceptance snapshot and is
not a new completeness target. Page 1 rendered 50 real rows. Page 2 rendered 50
rows with zero rendered-row overlap against page 1. Resource-name filter
`100281` returned 2,740 items and rendered 50 rows. Clear returned the Resource
count to zero and rendered no rows. The exact same assistant-owned game instance
remained `connected`, and the UI still displayed `Connected` after clear.

Runtime diagnostics for the accepted run contain zero console errors, console
warnings, page/unhandled errors, failed requests, HTTP >=400 responses and CSP
violations. Structured acceptance and screenshots are under
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/`.

## Deterministic verification

Passed before the live acceptance:

- `npm.cmd run check --prefix src/LWBridge.UI-0.3.17` — includes `LWB317_MAP_UI_INTEGRATION_CHECKS_OK`;
- `npm.cmd run build --prefix src/LWBridge.UI-0.3.17`;
- `npm.cmd run check:production-build --prefix src/LWBridge.UI-0.3.17`;
- `dotnet run --project tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj -c Release` — `LWB317_MAP_CHECKS_OK`;
- `dotnet build src/LWBridge.Map-0.3.17/LWBridge.Map-0.3.17.csproj -c Release --nologo`;
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --nologo`;
- disposable `dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release --nologo -o <task-temp>` — published `ProductionUi` identity/fingerprints revalidated and legacy `WebUi/index.html` remained present;
- full `LWBridge.Desktop.Checks.exe` — `ok=true`, `productionUiSelection=true`, no failures;
- `node tools/check_map_search_r8014.cjs`;
- `node tools/check_map_auto_r8015.cjs`;
- `node tools/check_scheduled_plunder_r8016.cjs`;
- `git diff --check`.

`ProductionUiSelectionChecks` specifically proves the normal canonical root,
legacy source preservation, proof-gated arbitrary `--ui-root`, explicit legacy
recovery, clear failure for missing canonical assets, bootstrap injection and
continued `window.chrome.webview` transport.

The compact structured validation record is
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/deterministic-validation.json`.

## Cleanup

The accepted run stopped the assistant-owned game instance through the native
lifecycle command and closed its Desktop process normally. Postflight process
counts are zero for Desktop, Last War, launcher and Overview helper. No recovery
journal or Map WAL/SHM remains. The installed package remained the official
content-version 22 package before and after the run with SHA-256
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.
See `cleanup.json` and the v22 pre/postflight JSON files in the task evidence
directory.

## Remaining state

The production frontend migration itself has no known blocker. Home, Automation,
Squads / AFK, City Layout, Hotkeys, Mini Games and Settings still contain the
accepted reconstructed/static or intentionally disabled behaviors documented by
Phase 1; this task did not claim functional recovery for those subsystems.

Map work remains under `LWB317-RE-MAP-001`: server jump,
restart/resume/cross-process saved browse context, other Map categories/actions
and final Map closure remain open. Treasure claim/status and Ghost preparation
remain fail-closed current-client gaps. Resource `GAME_UNIVERSE_COMPLETE` and
exact original private traversal equivalence remain `UNKNOWN`. Original
login/account/licensing reconstruction remains out of scope, and direct original
post-auth visual comparison remains blocked by that boundary.
