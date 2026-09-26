# LWB-R7-135 — Separate normal-production window smoke from fixture visual verification

**Date:** 2026-09-22
**Base revision:** `83eb1604b7d26c2949e20ba7ec663fdb54a86273`

R10 required production-mode behavior and fixture visual behavior to be rechecked as separate evidence paths. R7-134 already refreshed the fixture/browser/pixel side. R7-135 adds a reproducible normal-Release production-composition smoke without starting Last War or allowing state-changing owner commands.

## Maintained verification tool

`tools/check_normal_release_windows.ps1`

SHA-256:

`15314E2B566E77BD5D815051F7A45BC16780848F9714F5E14F9AC6B55FDAE55F`

The tool launches the real Release executable with `--owner-evidence`. This is not fixture/capture mode: the normal window constructs the production lifecycle service, control-pipe host, persistent map store and normal WebView presentation. Owner-evidence bootstrap suppresses auto-launch and blocks state-changing owner commands.

To avoid contaminating or consuming the user's browser-local UI state, the checker:

1. refuses to run if LWBridge.Desktop, LastWar, or LastWarLauncher is already active;
2. moves the existing `%LOCALAPPDATA%\LWBridgeRebuild\Presentation` directory to a unique temporary path;
3. creates a fresh Presentation directory;
4. launches normal Overview and Map Data windows separately;
5. checks WinForms responsiveness with `Process.Responding`, `IsHungAppWindow`, and `WM_NULL`/2-second `SendMessageTimeout`;
6. requires zero LastWar, LastWarLauncher and Overview-helper processes during both windows;
7. hashes `config.json` and `config.backup.json` before/after and requires byte identity;
8. closes each LWBridge window through normal `CloseMainWindow`;
9. deletes the fresh Presentation directory and restores the original directory in `finally`;
10. deletes unsanitized temporary owner-evidence logs after deriving the result.

A staged failure-first run exposed one checker-only race: both LWBridge windows had exited, but WebView2 still held `Presentation\EBWebView\lockfile` briefly, so an immediate recursive delete failed before restoration. The original profile was verified in the unique temporary backup (386 files / 41,490,264 bytes), the 4-file fresh profile was removed after release, and the original was restored before continuing. The maintained tool now retries temporary-directory removal for up to 20 seconds and writes its success result only after cleanup/restoration completes. It does not force-kill WebView2 to make the check pass.

## Fresh normal-production result

Release executable:

`src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.exe`

Canonical-worktree SHA-256 at base revision `83eb1604…`:

`1983E5A90FE93CD48246E39828027176E05ECBD6914558E55F064D908B9EB1BB`

Its assembly informational version is `1.0.0+83eb1604b7d26c2949e20ba7ec663fdb54a86273`. The staged index-only validation snapshot builds to SHA-256 `8A228D9AD619BB9406855A8CEBAECA3E10E397102B5153569CA43E50DE24A489` with informational version `1.0.0` because that snapshot intentionally has no `.git` metadata. The .NET SDK's embedded `SourceRevisionId` explains the binary hash difference; application source is identical.

Release build: **0 warnings / 0 errors**.

### Overview

- `Responding=true`
- `IsHungAppWindow=false`
- `WM_NULL` succeeded
- LastWar processes: 0
- launcher processes: 0
- Overview helper processes: 0
- sanitized evidence events: `session-start`, `owner-command-blocked`
- the only blocked owner write was `server_jump_history_import`

### Map Data

- `Responding=true`
- `IsHungAppWindow=false`
- `WM_NULL` succeeded
- LastWar processes: 0
- launcher processes: 0
- Overview helper processes: 0
- sanitized evidence events: `session-start`, `owner-command-blocked`, `city-search-response`, `city-render-observation`
- the only blocked owner write was `server_jump_history_import`
- the normal production Map Data page executed a real saved City search and rendered the correlated empty-state table

### Restoration

- real `config.json` SHA-256 unchanged
- real `config.backup.json` SHA-256 unchanged
- an existing Presentation profile was present and was restored after the run
- final LWBridge.Desktop processes: 0
- final LastWar processes: 0
- final launcher processes: 0
- final Overview-helper processes: 0

## Fixture/visual evidence remains separate

R7-134 remains the fixture/browser/pixel authority:

- 36 browser checks pass
- City-export removal regression passes
- 32 screenshot pairs pass
- all 28 strict pairs are pixel-identical
- the four intentional Map Data override pairs remain within their established bounds

R7-135 deliberately does not claim that fixture evidence is production evidence or vice versa.

## Exact build and verification commands

From the repository root:

```powershell
dotnet build src\LWBridge.Desktop\LWBridge.Desktop.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\check_normal_release_windows.ps1
```

Fresh runnable executable:

`src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe`

Normal user launch is the executable with no diagnostic arguments. Its stored startup preference may intentionally launch Last War; the passive verification command above is the no-game safety check.

## R10 effect

The R10 row **"Recheck production-mode behavior and the fixture visual matrix separately"** is closed.

The final integrated release gate is still open. In particular, this smoke does not close the human normal-user two-page walkthrough, active-scan app-restart cases, the requested three full authorized multi-server Auto cycles, population-dependent Ghost/Supplies proof, simultaneous real multi-account UI population, or state-changing Treasure/Truck/Dispatch/Alliance actions.

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-22-r7-normal-production-window-smoke.json`
