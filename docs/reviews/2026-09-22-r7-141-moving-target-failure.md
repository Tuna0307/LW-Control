# LWB-R7-141 — Moving-target vanished/replaced failure acceptance

**Date:** 2026-09-22
**Base revision:** `64a89069202fd61dec3c938b363ca3f5e54f4c2f`
**Acceptance case:** C07
**Status:** PASS_CURRENT_PLUS_HISTORICAL

## Goal

Close the acceptance rule:

> Coordinate jump / moving-target follow / vanished or replaced target -> correct actual map focus or explicit failure; no stale-target success.

Successful live navigation was already proven. R7-141 closes the missing vanished/replaced failure branches without consuming any gameplay action.

## Current host protocol acceptance

For requested server 2212 and march UUID `7654321090123`:

### Vanished target

A correlated game-side result with:

- `state=failed`
- `error=march_follow_completion_timeout`

must surface:

- code `MARCH_FOLLOW_FAILED`
- message `march_follow_completion_timeout`

One public Follow request is written. No hidden retry and no stale success are accepted.

### Replaced identity

A correlated failed result with `march_identity_mismatch` must surface explicit `MARCH_FOLLOW_FAILED`.

### Replaced/wrong server

A correlated failed result with `march_server_mismatch` must surface explicit `MARCH_FOLLOW_FAILED`.

### Forged success with wrong identity

A synthetically correlated `state=proven` response whose `marchUuid` differs from the requested UUID is rejected before success with:

`InvalidDataException: March Follow result did not match the active owned game session or requested march.`

This proves the host cannot turn a replaced target into stale-target success merely because the response says `proven`.

## Public service ownership recovery

The public `map_march_follow` path is also covered.

On one `ManualMapScanCommandService` instance:

1. the first Follow fails with `MARCH_FOLLOW_FAILED / march_follow_completion_timeout`;
2. the very next Follow with the same valid identity succeeds.

This proves the failure path releases the navigation ownership flag in `finally`; a vanished target cannot wedge later navigation behind `MAP_NAVIGATION_RUNNING`.

## Shipped Lua anti-stale contract

A permanent source-contract regression scopes itself to `pump_march_follow` in `tools/current_overview_bridge.lua`.

It requires the shipped bridge to retain:

- exact requested UUID lookup through `WorldMarchDataManager.GetMarch(request.marchUuid)`;
- fallback exact requested UUID lookup through `World.GetMarch(request.marchUuid)`;
- explicit `march_identity_mismatch` failure;
- explicit `march_server_mismatch` failure;
- `proven` only after the requested march is observed;
- explicit `march_follow_completion_timeout` when it never becomes observable.

The recovered native navigation window remains 5 seconds.

## Live success authorities

R7-141 does not induce a real target disappearance.

It composes the current failure evidence with already-live positive navigation evidence:

- **LWB-R7-061** — public coordinate jump live-proven on an owned session, including return to player;
- **LWB-R7-040** — Truck moving Follow live-proven through `JumpToMarchByUuid/GetMarchPos`;
- **LWB-R7-071** — Railway positive-row public Follow live-proven using the exact published `marchUuid`.

Thus C07 has both sides of the acceptance rule: live actual-focus success and current explicit no-stale-target failure.

## Permanent regressions

- `tests/LWBridge.Desktop.Checks/CurrentClientMapBlockSourceChecks.cs`
- `tests/LWBridge.Desktop.Checks/ManualMapScanCommandServiceChecks.cs`
- `tests/LWBridge.Desktop.Checks/MovingTargetFailureChecks.cs`

All execute in the default deterministic suite.

## Validation

Fresh R7-141 clean-worktree run:

- Release build: **0 warnings / 0 errors**
- all six deterministic groups: **true**
- deterministic failures: **[]**
- game running after checks: false
- launcher running after checks: false

## Acceptance effect

**C07 -> PASS_CURRENT_PLUS_HISTORICAL**

R7-141 does **not** claim a newly induced live disappearance. It proves the current failure behavior and anti-stale source contract, composed with existing live success evidence.

## Remaining ordinary partials

After R7-141, only two acceptance rows remain in ordinary `partial` state:

- B11 — native add/update/remove/movement transition matrix;
- F04 — final human normal-user built-executable walkthrough.

Population-, authorization- and implementation-blocked rows remain separate categories.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-moving-target-failure.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7141.json`
