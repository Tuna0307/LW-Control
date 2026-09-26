# R8-051 - fence automation_inspect at authorization/result-rewrite boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the recoverable host boundary of the retained read-only `automation_inspect` command without recreating excluded authorization-state inputs or guessing the native `allianceGarrison` rewrite.

## Native authority

Primary evidence:

- `automation_inspect` handler `0x14015DF53-0x14015F565`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- selected-profile runtime resolver `0x1402AE43C`;
- game-route helper `0x1403AD367-0x1403AD3B8`;
- shared game-call future `0x1400E4FAD`;
- generic JSON result converter `0x1402BB816`;
- retained frontend wrappers in `api-ClPPi2JT.js`, including the fixed `allianceGarrison` inspect call.

## Authorization-state dependency

Native awaits the shared authorization-state future before constructing the inspect request. If that state is unavailable, the exact public error is:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

The handler's request-building state carries authorization-derived material; nearby native vocabulary includes `premium` and `admin`. The exact role/authorization projection is intentionally not decoded because authorization/account-state recovery is owner-excluded.

R8-051 therefore does not substitute hard-coded role flags or frontend assumptions.

## Public request/game-call boundary

The handler extracts the public `task` field as an optional JSON string through native string-field helper `0x1401D2EE7`. The retained frontend normally supplies `{task:<name>}`; it also invokes the same command with `task: "allianceGarrison"`.

After selected-runtime resolution, native verifies the game route. Missing connectivity returns exact:

- code: `GAME_DISCONNECTED`
- message: `game disconnected`

The connected request uses:

- method: `inspectAutomationTask`
- result deadline: **5,000 ms** (`0x1388`)
- no handler retry loop.

The shared timeout path therefore yields `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: inspectAutomationTask`.

Native constructs an inspect request containing at least `task`; additional `options`/authorization-derived request semantics are not fully closed and are not guessed.

## Result handling

For the normal path the correlated provider result reaches the shared generic JSON converter (`0x1402BB816`).

`allianceGarrison` is explicitly special-cased after the provider result. Native compares the task name exactly and then parses/rebuilds result content rather than returning an unconditional raw pass-through. The recovered branch reads an `allies` collection and ally fields including `uuid`, `serverId`, and `uid`; later logic also references `updatedAt`.

The exact filtering/deduplication/enrichment/order semantics of that rewrite are not yet closed one-for-one. Returning the provider JSON unchanged for `allianceGarrison` would therefore be an observable deviation.

## Why no production implementation is added

Two independent dependencies prevent a strict implementation:

1. request construction depends on shared authorization state, whose account/role projection is owner-excluded;
2. `allianceGarrison` has a host-side result rewrite whose exact algorithm is still partial.

A generic `inspectAutomationTask` pass-through would silently get both boundaries wrong. R8-051 makes no runtime-code change and keeps `automation_inspect` fenced while preserving the already restored generic automation status/config work.

## Evidence classification

- authorization unavailable error: `EXACT_NATIVE`;
- direct `task` extraction: `EXACT_NATIVE`;
- game disconnected error: `EXACT_NATIVE`;
- provider method `inspectAutomationTask`: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- normal generic result conversion: `EXACT_NATIVE`;
- authorization/role-derived request details: `OWNER_EXCLUDED_UNKNOWN`;
- `options` request normalization: `UNKNOWN`;
- exact `allianceGarrison` result rewrite: `UNKNOWN`;
- runtime implementation: `FENCED`.