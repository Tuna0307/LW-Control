# Development guide

## Requirements

- Windows x64 for building and running the app: .NET 10 SDK, Node.js 22+/npm, Microsoft Edge
  WebView2 Runtime. Live game work also needs Python 3 and the verified Last War installation.
- On any OS, the UI checks (Node) and most game-script checks (Python + `pip install lupa`) can run
  without Windows. Checks that import `msvcrt`/`_winapi`, launch Edge, or need the local
  `artifacts/map-013-lua-site` Lua build run only on the Windows dev machine.

## Build and run

```powershell
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
```

The build first runs `npm ci` and `npm run build` in `src/LWBridge.UI-0.3.17`
(`-p:SkipCanonicalProductionUiBuild=true` skips this). It also copies every
`src/LWBridge.GameScripts/*` file listed in the csproj to `OverviewBridge/` /
`LiveResourceProbe/` in the output.

Run `Start LWBridge.cmd`, which rebuilds when HEAD changed and then starts the exe. You can also run
the exe directly. Use `--isolated-root <dir>` to keep app data away from your real profile. Auto
Launch is ON by default, so a normal start may launch the game.

## Tests

| Suite | Command | Where it runs |
|---|---|---|
| UI | `npm --prefix src/LWBridge.UI-0.3.17 run check` | Anywhere. Two checks (`check-r29-hotkeys-mini-responsive`, `check-r29-map-readable`) need Edge, so Windows only. |
| Desktop (C#) | `dotnet run --project tests/LWBridge.Desktop.Checks -c Release` | Windows |
| Game scripts (Python + Lua) | `python tests/GameScripts.Checks/run_all_checks.py --output <new-dir>` | Anywhere. Windows-only checks print SKIP; on Windows add `--strict`, plus `--original-dir <verified original package>` for `feature018_package_checks.py`. |
| Release packager | `python tools/release/test_package_release.py` | Anywhere |
| Packaged smoke | `pwsh -NoProfile -File tools/release/check_packaged_home.ps1 -Executable <publish>/LWBridge.Desktop.exe` | Windows. Isolated fixture boot/render/cleanup, no game. |

Baseline on 2026-10-27, Linux:

- UI: all jsdom checks pass.
- Game scripts: 37 pass and 16 skip on Linux. All 16 are Windows-only or need `--original-dir`;
  each skipped check gets past its imports when a stub `msvcrt` is provided.
- Packager tests: pass.

## Release

1. Merge the reviewed work to `main`, then commit and push. The packager refuses dirty or unpushed trees.
2. On Windows:
   ```powershell
   dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release -o artifacts/publish
   python tools/release/package_release.py --publish artifacts/publish --output artifacts/release-YYYYMMDD
   ```
   This writes `LW-Control-<commit12>.zip` and `package-receipt.json`. The ZIP holds `LW-Control-UI/`
   plus `PROGRAMME-RELEASE-INTEGRITY.json`, which lists hashes and the source commit. Packaging
   rejects test-account UIDs, databases, captures and research folders.
3. Smoke-test the extracted ZIP (fresh folder, `LWBridge.Desktop.exe`), then create a GitHub
   release with tag `lw-control-vX.Y-YYYY-MM-DD` and the ZIP attached. Update the download link
   in `README.md` and the release table in [HISTORY.md](HISTORY.md).

## After a game update

The app refuses to inject into an unknown client. To support a new game build:

1. Record the new hashes for the files checked in `src/LWBridge.Desktop/CurrentClientCompatibility.cs`
   and `src/LWBridge.GameScripts/current_client_compat.py` (LuaEntry and the other pinned `.luac` files).
2. Re-check the game Lua interfaces that the `feature018_*.lua` and probe modules call. The
   inspection helpers in `tools/research/map_009_offline/` (`inspect_current.py`,
   `execute_selected.py`, `compact_lua53.py`) can decode the installed package.
3. Run the full suites, then a live Home Launch→Connected→Close on the test account
   ([LIVE_TESTING.md](LIVE_TESTING.md)), before releasing.

## Conventions

- One canonical implementation per feature. No fake success, no placeholder providers.
- Any deliberate change from the original LWBridge behavior gets a row in
  [BEHAVIOR_DIFFERENCES.md](BEHAVIOR_DIFFERENCES.md), labelled OWN_DESIGN, OWNER_DIRECTION or
  CURRENT_CLIENT_ADAPTATION.
- `TreatWarningsAsErrors` is on for all C# projects (`Directory.Build.props`).
- The nine UI locales (`src/LWBridge.UI-0.3.17/src/locales/`) must stay complete;
  `check-ui-complete.mjs` enforces this.
- Never commit account data, test UIDs in product code, databases, screenshots of accounts, or
  third-party binaries. Put local evidence in the git-ignored `artifacts/`.
- Tests that touch game logic must say whether they are **controlled/inert** (fake game objects) or
  **genuine/native** (the real game). Only genuine evidence counts as a live pass.
