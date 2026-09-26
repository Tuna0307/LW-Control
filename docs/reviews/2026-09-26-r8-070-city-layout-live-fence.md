# R8-070 — fence City Layout Validate / Apply Start / Apply Cancel at authorization-state admission

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1

Fresh native xrefs correct the older 2026-09-24 “implementation-ready” assessment for three live City Layout commands. Under the current strict-parity directive, all three have a mandatory shared authorization-state dependency before selected-runtime/provider execution.

## Exact handlers

- `city_layout_apply_cancel`: `0x140113007-0x140113C01`
- `city_layout_apply_start`: `0x140183AFC-0x1401847DD`
- `city_layout_validate`: `0x1401869E7-0x140187506`

Each handler calls the shared authorization-state future `0x1400DC79E` before selected-profile runtime resolution `0x1402AE43C`.

## Validate

Request: `{baseRevision,placements}`.

Provider: `validateCityLayout`. Deadline: 10,000 ms. No handler retry loop. Normal result passes through the shared generic JSON converter. Frontend minimum result is `{valid,issues,totalMoves,temporaryMoves}`.

## Apply Start

Request: `{baseRevision,placements}`.

Native obtains a fresh City Layout snapshot and enforces `snapshot.isInCity`; false yields `CITY_LAYOUT_NOT_IN_CITY`. Provider: `startCityLayoutApply`. Deadline: 10,000 ms. Normal provider result uses the generic JSON converter. Frontend minimum result is `{accepted,status,issues?}`.

## Apply Cancel

Request: `{jobId}`.

Provider: `cancelCityLayoutApply`. Deadline: 5,000 ms. Normal result is converted generically and installed directly as ApplyStatus by the frontend.

## Shared failures and fence

All three inherit the recovered authorization-unavailable boundary `STATE_UNAVAILABLE / authorization state is unavailable` before provider work. Game-route absence yields `GAME_DISCONNECTED / game disconnected`; shared call timeout uses `LUA_CALL_TIMEOUT`.

Because authorization-state admission is owner-excluded, bypassing it would change original error precedence and recreating it would violate retained scope. No runtime route is added.

R8-070 moves `city_layout_validate`, `city_layout_apply_start`, and `city_layout_apply_cancel` from genuinely unclosed to audited/fenced. Remaining genuinely-unclosed retained frontend routing gaps: **10**.

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-070-city-layout-live-fence.json`.
