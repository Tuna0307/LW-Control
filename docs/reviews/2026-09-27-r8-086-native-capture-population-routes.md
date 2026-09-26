# R8-086 — recover native-capture getter population routes

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** safe-region resolved getter-to-native-record field routing. No production scanner change.

## Result

Both verified embedded proxies use identical getter-to-field routes for the nine game-facing methods already resolved by R8-084.

The point producer's temporary record base is `rbp-0xA0`. The march producer's temporary record base is `rbp-0xD0`. These bases align the observed producer writes with the exact R8-083 native-record offsets.

R8-086 closes these routes and their gates/transforms; it does not claim complete population semantics for all 57 point fields or all 25 march fields.
## Point routes

- `GetResType` populates nullable `resType` at value/presence offsets `0x68/0x6C`, only inside the producer's `pointType == 7` resource branch.
- `GetResLevel` populates nullable `resLevel` at `0x70/0x74` under the same `pointType == 7` gate.
- `GetWorldTreasureType` populates nullable `treasureType` at `0xA0/0xA4` after the runtime-class check for exact `TreasurePointInfo`. When that getter is unavailable, the producer has a direct-field fallback in the same class branch.
- `get_configId` populates nullable `cfgId` at `0x40/0x44` after the runtime-class check for exact `WorldSuppliesPoint`.

The class tests are not inferred from names: the producer compares the inline literal bytes for `TreasurePointInfo` and `WorldSuppliesPoint` before entering those getter paths.
## March routes

- `GetMarchCurPosIndex` preferentially populates nullable `pointIndex` at `0x10/0x14` only when its result is positive. If the result is non-positive/unavailable, an already-present pointIndex is preserved; otherwise the producer enters its direct-field fallback chain.
- `GetMaxHP` populates nullable `maxHp` at `0x38/0x3C` when the getter is available.
- `IsMonsterOrOrdinaryBoss` populates nullable `isMonster` at `0xA0/0xA1`, but the producer transforms the return value with `result > 0` before storing the boolean.
- `IsOrdinaryBoss` populates nullable `requiresRally` at `0xA2/0xA3` with the returned byte.
- `IsNormalType` populates nullable `normalType` at `0xA4/0xA5` with the returned byte.

The march record-base relation is also consistent with the five R8-083 optional-string slots: ownerUid at record offset `0xA8` is the producer string object at `rbp-0x28`, fixing the base at `rbp-0xD0`.
## Impact and limits

R8-086 converts the nine R8-084 resolver names from mere producer dependencies into exact native-capture field routes. This removes ambiguity for resource classification, treasure/config classification, march position/HP and the three march booleans.

Many producer-populated fields are still sourced through resolved raw field offsets, copied game objects, runtime-class-specific branches and helper transforms that are not yet mapped one-by-one. Those remain open.

This checkpoint does not inspect or cross protected native region `0x3F8E0-0x40A6D`. It does not recover `XluaBridgeMapScanTick` traversal/order/coordinates, per-tick request pacing, retry/backoff or protected acknowledgement semantics.

## Verification

Hash-gated verifier: `tools/inspect_lwbridge_native_capture_population_routes.py`

Durable evidence: `evidence/lwbridge-implementation/2026-09-27-r8-086-native-capture-population-routes.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner behavior is changed.
