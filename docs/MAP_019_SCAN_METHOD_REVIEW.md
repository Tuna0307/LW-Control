# MAP-019 scanner method review — phase 1

Status: **PARTIAL — current owned-session live measurements blocked by an independent running game in the selected installation.**

Base: `codex/feature-recovery-018` at `ee3ebe0221a0049512fdb7d23acb610d009db83b`.
Work branch: `codex/map-scan-method-019`. Date: 2026-10-23 (Asia/Singapore).
No production behavior changes. No merge, release or ZIP is authorized in this phase.

This is an investigation checkpoint. Historical evidence re-audit, current shipped-source
analysis, request-plan prototype and validation are in progress; none is a fresh native
speed/accuracy result. The final checkpoint will replace the sections below with the
bounded measured findings and exact remaining dependencies.

## Admission dependency

Reproduce: `python tools/map_019_review/preflight.py --output artifacts/map019/admission-before.json`.
Retained receipt: `artifacts/map019/admission-before.json` (private/ignored).
The production helper's read-only `require_no_selected_game_process` guard returned:

> the selected LastWar installation is already running (pid 13452); bounded acquisition requires a helper-owned launch session

The observed game was already running before MAP-019, alongside the owner's packaged
FEATURE018 test app. This worker did not start or acquire that session. The guard runs
before backup/install/recovery in `run_overview_bridge.py:run_start`; bypassing it or
stopping/adopting the independent session would violate the ownership boundary.
No live benchmark, RPC, input, script replacement or game action was attempted.
This is a session-ownership dependency, not a tool approval rejection or protocol failure.

## Recommendation at this checkpoint

Keep the current production method. New source analysis and offline planning cannot
qualify a replacement. In particular, prior MAP-013 camera-free failures are specific
to their then-tested API/arguments; MAP-015's subsequently accepted direct-message
method supersedes any blanket claim that camera movement is required.

Pending: one table per assigned question, historical receipt re-audit, current route
inventory, opt-in request plan and validation results. Fresh same-session A/B/drift,
bytes, game FPS/memory and candidate timings remain unmeasured while admission is blocked.
