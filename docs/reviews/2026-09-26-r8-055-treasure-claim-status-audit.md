# R8-055 - strict audit map_treasure_claim_status

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the native read-only Treasure claim-status host boundary and classify the existing current-client reconstruction without enabling protected `map_treasure_claim` execution.

## Native authority

Primary evidence:

- `map_treasure_claim_status` handler `0x14015590D-0x14015636E`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- selected-profile runtime resolver `0x1402AE43C`;
- provider method `getTreasureClaimStatus`;
- shared game-call future `0x1400E4FAD`;
- status/cache projector `0x1402317B6-0x14023195E`;
- transactional Treasure-state cache updater `0x14026B285-0x14026BE40`;
- generic provider JSON converter `0x1402BB816`;
- retained frontend `map_treasure_claim_status()` wrapper and Treasure polling flow.

## Public admission/provider call

The frontend supplies no feature payload. Native first awaits shared authorization state. Unavailable state is exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

After admission, native resolves the selected profile runtime and performs one provider call:

- method: `getTreasureClaimStatus`
- deadline: **5,000 ms** (`0x1388`)
- no handler retry loop.

The shared timeout path is `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getTreasureClaimStatus`.

R8-055 does not reconstruct authorization/account state; that remains owner-excluded.

## Native provider-result handling

The provider result is inspected for:

- `playerUid`;
- `states`.

`states` accepts the native collection representations handled by the projector; the frontend likewise merges either an array or object-values collection.

Native then updates the persisted Treasure claim-state cache as a side effect before returning the public result. Recovered state fields used by the cache updater include:

- `serverId`;
- `ownerUid`;
- `uuid`;
- `expireTime`;
- `claimStateUpdatedAt`.

The updater serializes accepted state rows, removes positive expired cache rows at/before the sampled clock, upserts `treasure_claim_states` transactionally, and commits. Serialization failure uses native `MAP_DATA_INVALID` with the `serialize treasure claim state: ` detail prefix.

Critically, the cache-updater result is used only for success/error control. After a successful cache update, the handler sends the **original provider JSON value** through the generic converter. The public command therefore preserves provider-owned fields instead of rebuilding a fixed host DTO.

The retained frontend consumes `playerUid`, `allianceId`, `states`, and also recognizes optional `batch` during claim polling. `batch` is provider-owned; it is not synthesized by the host cache updater.

## Current rebuild comparison

`ManualMapScanCommandService.InspectTreasureStateAsync()` currently routes `map_treasure_claim_status` through the generic current-client Treasure inspection path. That is useful live-read plumbing but is not strict native parity.

Current deviations include:

1. rebuild-only host admission/serialization guards before the provider route: `MAP_SCAN_CLOSED`, `SCAN_RUNNING`, `GAME_OPERATION_IN_PROGRESS`, and server-match checks; the native status handler itself proceeds from authorization/runtime admission to the status provider call;
2. current-client inspection timeout is **8 seconds**, versus native status deadline **5 seconds**;
3. the rebuild reconstructs exactly `{playerUid, allianceId, states}` instead of returning provider JSON unchanged;
4. `CurrentClientTreasureInspectionResult` cannot retain optional provider-owned fields such as `batch` or future/unknown extras;
5. the status path is coupled to generic page-refresh machinery and live-server derivation rather than a dedicated native-style status provider call;
6. cache writes occur only when rebuilt `states.Count > 0`; native cache maintenance still performs its transactional cleanup/update sequence for the status result.

The existing `treasure_claim_states` schema/upsert/expiry model remains strongly evidence-backed and broadly matches the native cache side effect. The command wrapper/provider ownership does not.

## Protected execution boundary

`map_treasure_claim` remains deliberately absent. R8-055 does not recover or enable protected claim scheduling, lucky-slot prioritization, scout-slot reservation, march/collection execution, or terminal claim orchestration.

## Evidence classification

- no feature payload: `EXACT_NATIVE`;
- authorization unavailable error: `EXACT_NATIVE`;
- provider method `getTreasureClaimStatus`: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- `playerUid` / `states` cache-input extraction: `EXACT_NATIVE`;
- transactional claim-state cache update: `EXACT_NATIVE` / existing store broadly equivalent;
- original provider JSON public return: `EXACT_NATIVE`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- protected claim execution: `PROTECTED_UNKNOWN`;
- current rebuild status route: `EQUIVALENT_REIMPLEMENTATION / DEVIATION`, not exact parity.