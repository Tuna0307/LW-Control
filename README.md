# LW-Control

A Windows desktop helper for **Last War: Survival**. It rebuilds the commercial tool LWBridge 0.3.17
(without its login/licensing system) and adds improvements learned from the LwTool client.

**Latest release: [v1.2 (2026-10-27)](https://github.com/Tuna0307/LW-Control/releases/tag/lw-control-v1.2-2026-10-27)**:
[download the ZIP](https://github.com/Tuna0307/LW-Control/releases/download/lw-control-v1.2-2026-10-27/LW-Control-lwtool-improvement-f93c3b9a2e56.zip).
Extract it into a fresh writable folder and run `LW-Control-UI/LWBridge.Desktop.exe`. Requires the
.NET 10 Desktop Runtime (x64), Microsoft Edge WebView2 Runtime, Python 3 and the supported Last War
PC client.

## Features

| Tab | What it does |
|---|---|
| Home | Launch, connect to and close the game per profile, with auto-reconnect and repair |
| Map Data | Fast whole-map scans of cities, resources, monsters, events, Trucks, Railway and more; filters, Jump, Auto scans, City XLSX export |
| Automation | Alliance help/donations/gifts/gathering, trade station, treatment, official application, Secret Task, Ghost Ops, chat rewards, Treasure |
| Squads / AFK | Monster AFK, shared AFK profiles, stamina items, Alliance Drill, Gathering |
| City Layout | Move and swap buildings |
| Hotkeys | Q/W/E/R attack, recall, shields, equipment presets, relocation, frontline reinforcement, G troops |
| Mini Games | Food House auto clear, Black Market chests |
| Settings | Nine UI languages, theme, in-game FPS/ping |

What is fully verified and what is only tested offline: see **[docs/STATUS.md](docs/STATUS.md)**.

## Repository layout

```
src/
  LWBridge.Desktop/        Windows app (C#, WinForms + WebView2): commands, game lifecycle, pipe, Map engine
  LWBridge.UI-0.3.17/      React frontend (built into the app's ProductionUi/)
  LWBridge.Map-0.3.17/     Map library (contracts, store, export)
  LWBridge.GamePipeAdapter/ DLL loaded in the game for the pipe connection
  LWBridge.GameScripts/    Python launch helpers + Lua that runs inside the game (shipped with the app)
tests/
  LWBridge.Desktop.Checks/ C# checks for the app (Windows)
  GameScripts.Checks/      Python checks that run the real Lua with fake game objects
tools/
  release/                 Release packager and packaged smoke test
  live-harness/            Scripts that drove live tests of the packaged app (Windows)
  research/                Game-package inspection and Map research helpers
  rea-mcp/                 Local reverse-engineering MCP tool config
docs/                      Status, architecture, development, live-testing rules, history
```

## Build and test

```powershell
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release   # also builds the UI
npm --prefix src/LWBridge.UI-0.3.17 run check
dotnet run --project tests/LWBridge.Desktop.Checks -c Release
python tests/GameScripts.Checks/run_all_checks.py --output artifacts/checks-run
```

Full instructions, including releases: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
Working on this with an AI agent? Start with [AGENTS.md](AGENTS.md).
