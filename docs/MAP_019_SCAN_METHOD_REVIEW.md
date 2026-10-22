# MAP-019 scanner method review — phase 1

Status: **IN_PROGRESS — fresh owned-session measurements underway.**

Base: `codex/feature-recovery-018` at `ee3ebe0221a0049512fdb7d23acb610d009db83b`.
Work branch: `codex/map-scan-method-019`. Date: 2026-10-23 (Asia/Singapore).
No production behavior changes. No merge, release or ZIP is authorized in this phase.

This is an investigation checkpoint. Historical evidence re-audit, current shipped-source
analysis, request-plan prototype and validation are in progress; none is a fresh native
speed/accuracy result. The final checkpoint will replace the sections below with the
bounded measured findings and exact remaining dependencies.

## Live authority and admission

The owner subsequently explicitly authorized closing the current game and app for testing.
The previous admission dependency below is resolved, not a remaining blocker. The worker
normally closed the exact packaged app, which exited its game and restored the scripts;
`artifacts/map019/authorized-owner-app-close.json` and
`artifacts/map019/authorized-session-recovery.json` record that cleanup. The latter admits
the restored original 3/27 package. New own-session work remains background-only,
read-only and free of mouse/keyboard input. No game actions are authorized.

Reproduce: `python tools/map_019_review/preflight.py --output artifacts/map019/admission-before.json`.
Retained receipt: `artifacts/map019/admission-before.json` (private/ignored).
The production helper's read-only `require_no_selected_game_process` guard returned:

> the selected LastWar installation is already running (pid 13452); bounded acquisition requires a helper-owned launch session

The observed game was already running before MAP-019, alongside the owner's packaged
FEATURE018 test app. This worker did not start or acquire that session. The guard runs
before backup/install/recovery in `run_overview_bridge.py:run_start`; bypassing it or
stopping/adopting the independent session would violate the ownership boundary.
At the initial checkpoint no live benchmark was attempted. That historical guard was a
session-ownership dependency, not a tool approval rejection or protocol failure.

Fresh initial City control: `artifacts/map014-lead-live/native-benchmark-city-20261023-010948`.
Two direct rounds took 22.1977841 s and 21.4574106 s; exact Stop and original-script
restoration succeeded (`final.json`), with camera checkpoints matching each round.
Reproduce with background flag `LWBRIDGE_FEATURE018_BACKGROUND_ONLY=1`,
`LWBRIDGE_MAP015_BENCH_METHODS=direct,direct`, `LWBRIDGE_MAP014_BENCH_ROUNDS=2`, then
`python tools/map_014_lead_native/run_owned.py benchmark-city`.

## Recommendation at this checkpoint

Keep the current production method. New source analysis and offline planning cannot
qualify a replacement. In particular, prior MAP-013 camera-free failures are specific
to their then-tested API/arguments; MAP-015's subsequently accepted direct-message
method supersedes any blanket claim that camera movement is required.

Pending: one table per assigned question, historical receipt re-audit, current route
inventory, opt-in request plan and validation results. Fresh same-session A/B/drift,
bytes, observed game frame timing/memory and candidate timings remain pending in the live matrix.
