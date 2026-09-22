# Home / Overview — current status

**Current through:** `LWB-R7-147`, 2026-09-22
**Canonical acceptance source:** `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7145.json`

This page is the current owner/auditor entry point for the Home / Overview tab. Older Home recovery and review documents remain historical evidence and should not be read as current status unless this page links to them.

## Current result

All ordinary Home acceptance cases A01-A12 are closed at their documented evidence scopes. There is no known ordinary Home implementation defect in the current acceptance matrix.

| Area | Current status | Main evidence |
|---|---|---|
| No-game startup / auto-launch off | PASS | A01 current + historical composition |
| Startup reconcile / auto-launch | LIVE-PROVEN | A02, R7-089 |
| Game-root picker / validation / persistence | PASS | A03 |
| Duplicate Launch / Refresh during launch | OFFLINE-TESTED | A04, R7-091 |
| Managed vs unmanaged process ownership | OFFLINE-TESTED | A05, R7-092 |
| Close Game timing / rollback | OFFLINE + LIVE startup-close | A06, R7-095/R7-128 |
| Automatic reconnect | OFFLINE-TESTED | A07, R7-093 |
| Path / permission / ABI / readiness faults | OFFLINE-TESTED | A08, R7-094 |
| 20-cycle Start/Close stress | LIVE-PROVEN | A09, R7-089 |
| Startup failure/rollback matrix | OFFLINE-TESTED | A10, R7-090 |
| Refresh Status / authenticated bridge / pending | CURRENT + LIVE composition | A11, R7-127 |
| Same/cross-server navigation | LIVE-PROVEN composition | A12, R7-133 + historical travel proof |

## User-visible release path

`LWB-R7-143` drives the actual built Release executable twice with zero application arguments, using OS mouse input to navigate Overview -> Map Data -> Overview, normal close, restart, and repeat. The shipped active-navigation state is observed; no `--view`, capture, probe, replay, backend shortcut, or DOM injection participates in the two acceptance runs.

`LWB-R7-135` separately verifies normal production-composition window responsiveness. `LWB-R7-128` live-proves startup-close rollback and the repaired native pipe/window responsiveness boundary.

## What does not need owner retesting now

Normal Home Launch/Close, startup ownership, reconnect policy, status refresh semantics, game-root validation, process ownership, and ordinary Overview/Map navigation do not currently need another owner retest unless a later code change touches those paths or the official client changes incompatibly.

## Remaining external Home-adjacent validation

The only notable availability gap is **simultaneous real multi-account UI population**. Deterministic multi-profile selection and real single-session transport/Stop are proven separately, but several simultaneously active real accounts have not been available for one integrated live UI population test.

This is an availability/acceptance gap, not a known Home defect.

## Primary source trail

- `docs/lwbridge-overview-recovery.md` — cumulative recovery ledger.
- `docs/reviews/2026-09-22-r7-143-normal-user-restart-walkthrough.md` — original F04 normal-user GUI acceptance.
- `docs/reviews/2026-09-22-r7-145-doc-evidence-self-audit.md` — current documentation/self-audit and verifier hardening.
- `evidence/lwbridge-implementation/2026-09-22-r7-normal-user-restart-walkthrough-autolaunch.json` — current F04 rerun with owner auto-launch safely suppressed/restored externally.
- `evidence/lwbridge-implementation/2026-09-21-r7-home-a11-live-transport-ui.json` — authenticated bridge/status/active-profile proof.
- `evidence/lwbridge-implementation/2026-09-21-r7-normal-window-close-cancellation.json` — live startup-close rollback/window proof.
- `evidence/lwbridge-implementation/2026-09-20-r7-overview-twenty-cycle-stress.json` — 20-cycle lifecycle proof.
