# LWBridge recovery / rebuild

**Current checkpoint:** `LWB-R7-145` plus the final audit-readiness front-door cleanup.
**Branch:** `research/offline-controller`.

This repository is the evidence-first LWBridge rebuild for the Last War PC client. The remote/directory name `LW-Control` is historical naming; it does not define feature authority.

The rebuild distinguishes evidence rigorously:

- **RECOVERED** — established from verified original LWBridge/current-client artifacts.
- **IMPLEMENTED/OFFLINE-TESTED** — implemented and tested without claiming a live game outcome.
- **LIVE-PROVEN** — observed against the identified current Last War client.
- **UNKNOWN/BLOCKED** — unresolved behavior that must not be filled in by guessing.

## Current status

Ordinary **Home / Overview** and **Map Data** functionality is technically mature at the scopes in the current 47-case matrix. The current matrix has **0 ordinary `partial` rows**.

The remaining non-pass work is deliberately separated from ordinary implementation defects:

- Ghost positive-row population proof is owner-deferred until 2026-09-24.
- Supplies positive-row population is still unavailable in the current live rechecks.
- Treasure protected claim-scheduler semantics remain blocked behind the preserved SB-79 boundary; public claim stays unrouted.
- Live Truck/Dispatch plunder and Alliance message delivery require suitable targets and explicit authorization.
- Simultaneous real multi-account UI population still requires several usable live accounts/sessions.
- Final integrated release acceptance remains a separate release-level gate.

City Excel export is intentionally retired by owner and is not unfinished work.

## Start here

Every AI/contributor must read [`AGENTS.md`](AGENTS.md) first. It contains mandatory evidence, recovery, safety, testing, and Git-delivery rules.

Then use this reading order:

1. [`docs/README.md`](docs/README.md) — canonical documentation index.
2. [`docs/implementation-handoff.md`](docs/implementation-handoff.md) — concise current continuation state.
3. [`docs/tabs/home.md`](docs/tabs/home.md) — current Home / Overview status.
4. [`docs/tabs/map-data.md`](docs/tabs/map-data.md) — current Map Data status and scan-performance audit.
5. [`docs/tabs/shared-release.md`](docs/tabs/shared-release.md) — shared runtime / Release status.
6. [`docs/lwbridge-project-status.md`](docs/lwbridge-project-status.md) — current project-manager summary.
7. [`docs/external-audit-guide.md`](docs/external-audit-guide.md) — instructions for an independent AI/reviewer.
8. [`evidence/lwbridge-implementation/README.md`](evidence/lwbridge-implementation/README.md) — current evidence navigation.
9. [`BACKLOG.md`](BACKLOG.md) — current remaining queue only.
10. [`task.md`](task.md) — durable product requirements and 47-case acceptance contract.

The current machine-readable acceptance source is:

`evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7145.json`

## Scan performance

On the standard current 1000×1000 world, production automatically selects the proven Fast strategy at concurrency 20; Zombie Boss-only uses its dedicated LOD2 strategy. R7-130 live evidence measured representative Truck at ~74.7 s, Monster at ~77.8 s, and all-eight at ~77.9 s with complete 2,500/2,500 logical-block coverage and zero failed/unread in those runs.

The supported conclusion is: **no additional evidence-backed safe speed optimization is currently known**. This does not claim future software can never be faster.

## Historical evidence and pruning policy

Dated reviews, recovery ledgers, and machine-readable evidence are retained when they contain unique provenance or are referenced by historical acceptance evidence. Superseded status wording must not be treated as current project state; current status comes from the pages listed above and the latest acceptance matrix.

The repository was audited for cleanup before external review. Exact duplicate implementation-evidence files were not found. Old-looking files that remain are kept because they preserve unique recovery/test provenance, satisfy historical references, or support reproduction. Do not delete them merely to reduce file count.

## Build and verification

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-build
```

Additional browser, normal-window, restart/navigation, and live checks are recorded in the current review/evidence index. Preview/capture modes must remain isolated from live game actions, and process existence alone is not proof of bridge readiness.
