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
