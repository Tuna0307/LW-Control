# LWBridge 0.3.1 recovery

This repository is now **LWBridge-only**. Treat the directory and remote repository name as historical naming; they do not define the product or feature authority.

The reference application is `lwbridge-0.3.1.exe` with SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

The goal is a one-for-one recovery of LWBridge behavior where evidence permits it. Keep four evidence states distinct:

- **RECOVERED** — statically recovered from the verified LWBridge application or embedded assets.
- **IMPLEMENTED/OFFLINE-TESTED** — implemented in the rebuild and verified without claiming a live game result.
- **LIVE-PROVEN** — observed against the current installed Last War client with authoritative outcome evidence.
- **UNKNOWN/BLOCKED** — still unresolved; do not fill gaps by guessing.

## Current implementation

- Recovered React/Vite UI is reproduced in WebView2 with the original feature pages, themes, icons, and nine languages.
- Login/account/license presentation has been removed from the rebuild.
- A real JavaScript-to-C# command/event boundary exists with request/session correlation, profile scoping, origin validation, cancellation, and structured failures.
- Game-root discovery/validation, local configuration, process inspection, startup/reconnect preference storage, and fixture/live separation are implemented in part. The current audit records four remaining persistence/request-lifetime defects.
- Launch remains fail-closed until the recovered bootstrap contract is complete.
- Map Scan request normalization is implemented for all eight recovered data types, with `normal=8` and `fast=20` concurrency. Production scanning remains gated on a verified bridge-ready game session.
- SQLite schema, explicit-key storage, persistent player marks and server-scoped clear have offline tests. Search/options/export and real capture are still unfinished.
- A repeatable read-only official-client inspector records current runtime versions, hashes, PE structure, packaged containers, hot-update state, and launcher lifecycle evidence.

## Start here

Read [docs/README.md](docs/README.md) first. It is the documentation index and defines the current reading order.

The two active planning files are:

- [task.md](task.md) — primary instructions, full Overview + Map Data requirements and acceptance tests.
- [BACKLOG.md](BACKLOG.md) — current progress checklist and ordered remaining work; formerly `TASKS.md`.

Read [the current project review](docs/lwbridge-project-status.md) for audited progress, reproduced defects and evidence limits. Keep these roles distinct; do not recreate a second similarly named task file.

## Build and verification

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
./tools/capture_lwbridge_ui.ps1
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
```

Normal development must keep preview/capture mode isolated from live game operations. A process merely existing is not proof that the LWBridge runtime is connected or ready.
