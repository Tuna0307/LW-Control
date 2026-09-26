# R8-051 - fence automation_inspect at authorization/result-rewrite boundary

> **Superseded correction (R8-091, 2026-09-27):** direct handler tracing proves `inspectAutomationTask` is sent only `{task}`. The earlier claim that request construction carried authorization-derived `premium` / `admin` material was an adjacency inference and is withdrawn. The shared authorization-state admission and incomplete `allianceGarrison` rewrite remain valid blockers.

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

R8-091 later proves the nearby `premium` / `admin` vocabulary is not part of this handler's provider request. The handler still awaits shared authorization state before request construction, but `inspectAutomationTask` itself is built with only `task`.

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

R8-091 closes the provider request shape: native constructs `inspectAutomationTask` with exactly the `task` entry; no `options`, `premium`, or `admin` entry is added by this handler.

## Result handling

For the normal path the correlated provider result reaches the shared generic JSON converter (`0x1402BB816`).

`allianceGarrison` is explicitly special-cased after the provider result. Native compares the task name exactly and then parses/rebuilds result content rather than returning an unconditional raw pass-through. The recovered branch reads an `allies` collection and ally fields including `uuid`, `serverId`, and `uid`; later logic also references `updatedAt`.

The exact filtering/deduplication/enrichment/order semantics of that rewrite are not yet closed one-for-one. Returning the provider JSON unchanged for `allianceGarrison` would therefore be an observable deviation.

## Why no production implementation is added

Two independent dependencies still prevent a strict implementation:

1. the handler requires the shared authorization-state admission before game routing, and the rebuild does not yet implement the original authorization-state producer;
2. `allianceGarrison` has a host-side result rewrite whose exact algorithm is still partial.

A generic `inspectAutomationTask` pass-through that bypasses authorization admission or skips the `allianceGarrison` rewrite would still diverge. The command remains fenced.

## Evidence classification

- authorization unavailable error: `EXACT_NATIVE`;
- direct `task` extraction: `EXACT_NATIVE`;
- game disconnected error: `EXACT_NATIVE`;
- provider method `inspectAutomationTask`: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- normal generic result conversion: `EXACT_NATIVE`;
- provider request shape `{task}`: `EXACT_NATIVE`;
- `premium/admin/options` provider fields in this handler: `DISPROVEN_BY_R8_091`;
- exact `allianceGarrison` result rewrite: `UNKNOWN`;
- runtime implementation: `FENCED`.