# Tools

Nothing in `tools/` ships with the app. Shipped game scripts live in `src/LWBridge.GameScripts/`.

| Folder | Purpose | Runs on |
|---|---|---|
| `release/` | `package_release.py` builds the source-pinned release ZIP from a fresh publish (see docs/DEVELOPMENT.md). `test_package_release.py` holds its inert tests. `check_packaged_home.ps1` is an isolated packaged boot/render smoke test. | Packager tests anywhere; packaging/smoke on Windows |
| `live-harness/` | Scripts that drove the real packaged app against the live game: `run_ui.py`, `run_owned.py` + `Native.csproj`, `feature018_live*.py`, `tabs_checkpoint.py`, `home_stability_*.py`, `live_ui_deploy.py`, plus Map receipt auditors and their `test_*.py`. **Read docs/LIVE_TESTING.md first** (it also maps each app proof switch to its driver). Several scenarios expect earlier receipts under `artifacts/` on the original dev machine. | Windows |
| `research/map_009_offline/` | Installed game package decoding (`inspect_current.py`, `compact_lua53.py`, `execute_selected.py`, `rgmd_il.py`), used by several game-script checks and by the After-a-game-update steps. Also the private wrapper trace (`map013_wrapper_trace.lua`, `run_map013_owned_observer.py`) and managed packet sampler that `run_owned.py` uses, and a few MAP-012/013 audits. Tests that read the installed game fail without it. | Mostly Windows |
| `research/map_014_il_decode/` | IL2CPP metadata/disassembly helpers used by the attack-action checks. | Anywhere |
| `research/map_019_review/` | Map scanner method review (MAP-019): benchmarks, audits, optional `run_owned.py` scenarios. | Mixed |
| `rea-mcp/` | Pinned project-local REA reverse-engineering MCP server (see its README). `.mcp.json` and `.codex/config.toml` hold absolute paths for the owner's Windows checkout. | Windows |
