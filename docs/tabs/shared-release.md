# Shared runtime / Release — current status

**Current through:** `LWB-R7-147`, 2026-09-22

This page covers behavior shared by Home and Map Data: the native host, generated frontend, persistent configuration, authenticated game bridge, window responsiveness, restart behavior, and release acceptance.

## Current release baseline

- Branch: `research/offline-controller`.
- Current checkpoint: `LWB-R7-147`; parent revision `91d07d04ada09b1cfd143c6171a0d45c19760828`.
- Installed game-side package at the last live audit: version 20.
- Canonical current matrix: `evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`.
- 47 acceptance cases are tracked; ordinary status `partial` count is zero.
- Remaining non-pass categories are population-, authorization-, or explicitly blocked-implementation cases, plus the owner-retired Excel-export case.

## Shared correctness

| Area | Current result |
|---|---|
| Generated recovered frontend | Maintained generator + browser checks |
| Theme/language/layout | Current isolated browser/capture acceptance |
| Normal Release window | Responsive production-composition smoke |
| Normal user navigation/restart | R7-143 accepted path + R7-145 auto-launch-safe verifier rerun |
| Config/profile persistence | Current deterministic + browser coverage |
| Authenticated bridge route | Live-proven read-only `getStatus` path |
| Request/session ownership | Stale/duplicate/foreign identity rejection covered |
| Startup/close rollback | Deterministic matrix + live close-during-start proof |
| Installed package restoration | Repeated live checkpoints verify exact hashes after owned sessions |

## Evidence labels

- `RECOVERED`: established from identified original/static artifacts.
- `IMPLEMENTED/OFFLINE-TESTED`: rebuild code plus stated offline tests.
- `LIVE-PROVEN`: observed against the identified current client.
- `PASS_CURRENT_PLUS_HISTORICAL`: current deterministic proof composed with earlier live authority.
- `PASS_CURRENT_NORMAL_USER`: real built Release path without application test mode.
- `UNKNOWN/BLOCKED`: missing contract is intentionally not invented.

## Current remaining release gates

The matrix still records four `partial_population` cases, state-changing action/authorization cases E01-E06, and one owner-retired case. Those are not ordinary Home/Map coding defects.

The repository must not silently promote any of them until the required live population, target, authorization, or recovered protected contract exists.

## Validation expectations

For a documentation-only checkpoint, at minimum verify Markdown/JSON link integrity, current matrix invariants, `git diff --check`, and exact staging. For any code change, also run the maintained frontend generator check, Release build, deterministic desktop checks, relevant browser checks, and live proof when the changed behavior requires it.

Current normal validation entry points include:

- `python tools/build_lwbridge_frontend.py --check`
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release`
- `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-build`
- maintained browser/check scripts referenced by the current review evidence.

## Audit rule

Historical files are evidence, not current instructions. When an older document says a feature is pending but the current matrix says it is passed, the matrix plus later linked evidence supersede the old status statement while preserving the old observation as provenance.
