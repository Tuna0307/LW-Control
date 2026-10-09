# LW-Control application candidate

A runnable Windows application reconstructed from LWBridge 0.3.17. This is an incremental release candidate, **not a completed 1-to-1 Home/Map clone**. Read [feature status](docs/FEATURE_STATUS.md) for the exact accepted scope and remaining gaps.

The main candidate contains production source and runtime dependencies. The large historical reverse-engineering archive stays on `research/offline-controller`; one small original frontend asset is retained because the locale checker uses it directly.

## Build and run

Requirements: Windows x64, .NET 10 SDK, Node.js/npm and Microsoft Edge WebView2 Runtime. Live game integration additionally uses Python and the currently verified Last War build; a game update must pass compatibility checks before use.

```powershell
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
```

Then double-click `Start LWBridge.cmd`, or run the executable directly. The original Auto Launch preference defaults to enabled, so normal startup can launch the game. An explicit independent application data root is supported:

```powershell
& ./src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.exe --isolated-root C:/LW-Control-data
```

## Verify the delivered Home slice

These checks use inert providers; they do not launch Last War:

```powershell
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
dotnet run --project tests/LWBridge.Desktop.Checks -c Release
pwsh -NoProfile -File tools/check_packaged_home.ps1
```

The packaged capture is explicitly an isolated fixture smoke test. It proves boot/render/cleanup, not original live parity. Actual mounted Home settings and persistence are recorded in [the delivery review](docs/HOME_DELIVERY_REVIEW.md).

## Publish

```powershell
dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release -o artifacts/application
```

Keep the whole output folder: it contains the canonical frontend, WebView dependencies, game-pipe adapter and Python/Lua helpers. The research directory is not required to build or launch it.

## Downloadable UI candidate

The reviewed UI release is built from this branch using `pwsh -NoProfile -File tools/package_ui_release.ps1` after a clean `dotnet publish`. This creates a ZIP under `artifacts/release/` with a complete publish folder, **Launch LWBridge.cmd**, a short `README-RELEASE.txt`, and the exact source commit. Extract the entire ZIP to a writable folder on Windows x64; install the **.NET 10 Desktop Runtime** and **Microsoft Edge WebView2 Runtime** if missing. Double-click **Launch LWBridge.cmd**. **The original Auto Launch setting is enabled by default**; on a normal user profile startup may launch Last War. The safe disabled-profile proof in the release review uses a separate test data root, not altered shipped defaults.

Release verifier (Windows; optional Microsoft Edge and dev-only Playwright required for the full browser sweep):

```powershell
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:release-ui
pwsh -NoProfile -File tools/check_packaged_home.ps1 -Executable "artifacts/application/LWBridge.Desktop.exe"
```

See [UI release review](docs/UI_MAIN_RELEASE_REVIEW.md) for the page/state matrix and unrecovered native behavior. The UI candidate is not an endorsement of complete native Home/Map parity, updater functionality or protected-original service access.
