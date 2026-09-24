# LWBridge recovery / rebuild

**Current checkpoint:** `LWB-R7-155` (2026-09-24).
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

- Ghost positive-row population remains unavailable: R7-152 current-v21 full scans on 2212/2175/2180/2185/2207 all completed cleanly with zero authentic Ghost rows.
- Supplies positive-row population remains unavailable: R7-153 current-v21 full scans on 2212/2175/2180/2185/2207/2213 all completed cleanly with zero authentic `WorldSuppliesPoint` rows.
- Railway direct Train-list acquisition is freshly live-proven on v21 by R7-155 across 11 sampled servers, all currently empty. Fresh v21 positive-row/Follow acceptance remains population-gated; historical v20 positive Follow remains provenance.
- Treasure protected claim-scheduler semantics remain blocked behind the preserved SB-79 boundary; public claim stays unrouted.
- Live Alliance message delivery requires a suitable target and explicit authorization. Scheduled Truck/Dispatch Plunder was retired by owner in R7-149.
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

`evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`

## Scan performance

On the standard current 1000×1000 world, production automatically selects the proven backend strategy. Truck/Railway-only uses the direct Train-list path. R7-151 current-v21 Dispatch/Secret Task uses an exact 68-request aligned wide AOI plan; a live server-2175 proof completed 2,500/2,500 blocks in 7.028 s with persisted/reopened rows. A separate official read-only Quick Find returns one Secret Task location in about 0.5 s and is used for immediate Auto Scan feedback without replacing the complete scan.

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
