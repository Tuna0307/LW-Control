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
- Selected-profile saved-resource browsing and reopen have recorded normal-window proof, distinct from live readiness.
- A bounded resource-only acquisition route has historical real-client evidence. The latest normal-window two-read/Search/render/reopen acceptance remains open; see [current readiness and tests](docs/user-test-checklist.md).
- Cancellation/cleanup ownership and exact result/query/render correlation repairs pass focused offline checks.
- Overview Launch remains fail-closed until its bootstrap contract is complete. Other-category acquisition, automatic/full-world scanning and complete export/action parity are unfinished.
- SQLite persistence, marks, scoped clear and supported search contracts have offline coverage; this is not full Map Data acceptance.
- A repeatable read-only official-client inspector records current runtime versions, hashes, PE structure, packaged containers, hot-update state, and launcher lifecycle evidence.

## Start here

**Current team:** Web handles implementation and automatic technical capture; the owner follows plain permitted UI steps and supplies screenshots/descriptions; PM audits. No separate Sol task. [Current prompts](docs/team-workflow.md) and [package preparation](docs/live-test-handoff.md). The recorder is implemented; review 15 requires two validation fixes before future owner use. The owner's empty-profile check is complete and needs no repeat. [Current audit](docs/lwbridge-project-status.md). Daybreak is unassigned.

**Every AI/contributor must first read [AGENTS.md](AGENTS.md).** These are mandatory user rules: reverse-engineer verified LWBridge and current official Last War artifacts before inferring behavior, never invent facts or numbers, document every successful recovery immediately, and commit/push each completed task or checkpoint to GitHub with verification.

Then read [docs/README.md](docs/README.md) for the evidence index and current reading order. Include `AGENTS.md` and `task.md` when handing work to another AI.

Project-related reverse-engineering tool discovery, installation and configuration are pre-authorized. A missing AI integration does not prevent standalone/headless tool use. Follow `AGENTS.md` for reproducible setup and accurate reporting of genuine environment restrictions.

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
