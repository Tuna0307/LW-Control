# Game scripts (shipped)

Every file here is copied into the app's output by `src/LWBridge.Desktop/LWBridge.Desktop.csproj`
(`OverviewBridge/` and `LiveResourceProbe/`). The app loads them by file name, so **do not rename**
them without updating the C# code and the csproj.

- `run_overview_bridge*.py`, `recover_overview_pending_current.py`, `run_live_resource_probe*.py`,
  `current_client_compat.py`: Python helpers that verify the installed client, back up and restore
  the game's script files, inject the bridge, and launch, stop and recover the game.
- `current_overview_bridge.lua`: Home connection (pipe, heartbeat, readiness) inside the game.
- `current_live_resource_probe.lua`: Map scanning inside the game.
- `feature018_*.lua`: all other tabs. `feature018_game_bridge.lua` registers them. See
  docs/ARCHITECTURE.md for the module-to-tab table.

Tests: `tests/GameScripts.Checks/`. The `feature018` prefix comes from the FEATURE-RECOVERY-018
work item that recovered these tabs.
