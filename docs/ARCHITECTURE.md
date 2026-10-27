# Architecture

LW-Control is a Windows desktop helper for the mobile/PC game *Last War: Survival*. It rebuilds
the behavior and UI of the commercial tool **LWBridge 0.3.17**, without its login/licensing
system. It also takes useful ideas from a competing tool, **LwTool**
(see [competitor-research](competitor-research/)).

```
 ┌──────────────────────────── LWBridge.Desktop.exe (WinForms + WebView2) ───────────────────────────┐
 │  ProductionUi/ (React app, built from src/LWBridge.UI-0.3.17)                                      │
 │        │  window.chrome.webview messages  (backendBridge.js ⇄ LWBridgeWindow.cs)                    │
 │        ▼                                                                                            │
 │  LWBridgeBackend → *CommandService.cs (one per feature)  ── SQLite stores (profiles, settings, map) │
 │        │                                                                                            │
 │        ├─ OverviewLifecycleService: start/stop/adopt/recover one game per profile                   │
 │        │     runs OverviewBridge/run_overview_bridge_current.py (Python helper)                    │
 │        ├─ LWBridgeControlPipe*: authenticated named pipe to the game (per session challenge)        │
 │        └─ Map scan engine / Feature018 services exchange commands with the owned game               │
 └────────────────────────────────────────────────────────────────────────────────────────────────────┘
                     │ installs bridge Lua into the game's script package, launches game,
                     │ restores the original files on exit
                     ▼
 ┌──────────── Last War game process (Unity + xLua) ─────────────┐
 │ LuaEntry (patched) → current_overview_bridge.lua (Home/pipe)   │
 │   + current_live_resource_probe.lua (Map scanning)             │
 │   + feature018_*.lua (Automation, Squads, Hotkeys, City, ...)  │
 │ LWBridge.GamePipeAdapter.dll: managed pipe client used by Lua  │
 └────────────────────────────────────────────────────────────────┘
```

## Projects

| Path | What it is |
|---|---|
| `src/LWBridge.Desktop/` | The Windows app (`net10.0-windows`, WinForms + WebView2). Hosts the UI, command services, profile/settings/map SQLite stores, game lifecycle, named-pipe server and Map scan engine. The UI it hosts is `ProductionUi/`. |
| `src/LWBridge.UI-0.3.17/` | React 19 + Vite frontend. The Desktop build runs `npm ci` and `npm run build` and copies the output to `ProductionUi/`. `scripts/check-*.mjs` are the UI tests (jsdom; a few use Edge through Playwright). `reference/original-0.3.17/` holds one original asset that a check compares against. |
| `src/LWBridge.Map-0.3.17/` | Map library: contracts, scan state machine, SQLite store, City XLSX export. |
| `src/LWBridge.GamePipeAdapter/` | `netstandard2.0` DLL loaded inside the game. It gives Lua a non-blocking framed pipe client. |
| `src/LWBridge.GameScripts/` | Every Python and Lua file the app ships (plus a README). `LWBridge.Desktop.csproj` copies them to `OverviewBridge/` and `LiveResourceProbe/` in the output. Python files are launch/install/restore helpers; Lua files run inside the game. |
| `tests/LWBridge.Desktop.Checks/` | C# console test program for the Desktop app (Windows only). |
| `tests/GameScripts.Checks/` | Python checks that run the real Lua in a Lua 5.3 VM (`lupa`) with fake game objects. Some need Windows. |

## Game connection (Home)

1. `run_overview_bridge_current.py` verifies the installed client (`current_client_compat.py`) and
   backs up the three original script files (`LWScripts.data`, `LWScripts.txt`, `version.txt`
   under `%USERPROFILE%/AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts`). It then
   injects a patched `LuaEntry` that loads our bridge Lua, and starts the official launcher/game.
2. In the game, `current_overview_bridge.lua` connects back through `LWBridge.GamePipeAdapter.dll`
   to the app's named pipe, using a per-session challenge. Heartbeats prove readiness.
3. On Close/exit the helper stops only the exact owned process (PID + path + start time) and
   restores the original files byte-for-byte. A recovery journal covers crashes.
4. Reconnect/repair is handled by `OverviewLifecycleRecovery`, with OFF/ON preferences per profile.

Each profile can own one game. The installed script package is per Windows user, so two real games
cannot run at the same time with one installation (behavior difference D-H18).

## Features inside the game

The `feature018_*` Lua modules (named after the FEATURE-RECOVERY-018 work item) implement the
non-Home tabs. `feature018_game_bridge.lua` registers them. Desktop services
(`Feature018OwnedGameExchange.cs`) exchange one JSON command at a time with the game. They use a
mailbox (`feature018-request.txt` / `feature018-response.json` in the runtime folder
`<app data>/overview-bridge`), and the game accepts a command only from the authenticated,
lease-fresh owner. Every action that changes the game checks the exact owner (profile, session,
PID, account UID and server) first, and never replays an uncertain request.

| Lua module | Tab / feature |
|---|---|
| `current_live_resource_probe.lua` | Map scanning (direct AOI requests, typed march sweep for Railway/Truck, detail hydration) |
| `feature018_automation_engine.lua`, `feature018_automation_native.lua`, `feature018_*_native.lua` (missions, trade, treatment, alliance resources/gather) | Automation tab |
| `feature018_chat_engine.lua` | Chat rewards and Treasure |
| `feature018_afk_engine.lua`, `feature018_afk_native.lua`, `feature018_stamina_engine.lua`, `feature018_drill_engine.lua`, `feature018_gather_native.lua` | Squads / AFK |
| `feature018_hotkey_game.lua`, `feature018_hotkey_actions.lua`, `feature018_attack_actions.lua`, `feature018_relocation_actions.lua`, `feature018_equipment_actions.lua` | Hotkeys and Equipment |
| `feature018_city_game.lua` | City Layout |
| `feature018_mini_game.lua`, `feature018_sheep_solver.lua` | Mini Games |

## Data

- App data: `%LOCALAPPDATA%/LWBridgeRebuild/`, or `--isolated-root <dir>` for tests. This holds the
  profile registry, per-profile settings and Map SQLite. Map scan data is cleared on every app start
  (D-F04).
- Nothing account-specific is committed. Live-test receipts go to the git-ignored `artifacts/`.

## Proof switches

`Program.cs` accepts many `--*-proof` switches, and the `LWBridgeWindow.*Proof.cs` partial classes
implement them, together with some `LWBRIDGE_*` environment variables. They let the live harness
(`tools/live-harness/`) drive the real packaged app without mouse or keyboard input. None run
without an explicit switch or variable. [LIVE_TESTING.md](LIVE_TESTING.md#proof-switches) maps
each one to its driver; removing the unused ones is a follow-up in [STATUS.md](STATUS.md).
