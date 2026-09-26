# R8-052 - fence city_layout_apply_status at authorization-state boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the observable read-only `city_layout_apply_status` host boundary without inventing authorization-state availability or live City Layout apply state.

## Native authority

Primary evidence:

- `city_layout_apply_status` handler `0x140122DA2-0x140123765`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- selected-profile runtime resolver `0x1402AE43C`;
- connected-route predicate/game-disconnect path;
- shared game-call future `0x1400E4FAD`;
- generic JSON result converter `0x1402BB816`;
- retained frontend wrapper `city_layout_apply_status`.

## Admission boundary

The retained frontend invokes `city_layout_apply_status` with no payload. Native first awaits the shared authorization-state future.

Unavailable authorization state returns exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

That admission dependency is owner-excluded and is not replaced with a synthetic always-available state.

## Game-call boundary

After the authorization-state dependency succeeds, native resolves the selected profile runtime and requires a connected game route.

Missing connectivity returns exact:

- code: `GAME_DISCONNECTED`
- message: `game disconnected`

The connected request is:

- method: `getCityLayoutApplyStatus`
- result deadline: **5,000 ms** (`0x1388`)
- no handler retry loop.

The shared timeout path therefore yields `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getCityLayoutApplyStatus`.

## Success/result handling

Native passes the correlated provider result through the shared generic JSON converter and returns it. No host-side result DTO or post-processing branch is visible in this handler.

The exact live provider status schema remains provider-owned and is not synthesized from the rebuild's draft state or frontend assumptions.

## Why no production implementation is added

`city_layout_apply_status` is read-only, but native still has an authorization-state admission dependency. The rebuild does not restore owner-excluded auth-state behavior, and the apply provider/start lifecycle is itself still missing.

Returning a fabricated idle status, bypassing the native authorization admission, or deriving status from draft persistence would all be observable deviations. R8-052 therefore makes no runtime-code change.

## Evidence classification

- authorization unavailable error: `EXACT_NATIVE`;
- game disconnected error: `EXACT_NATIVE`;
- provider method `getCityLayoutApplyStatus`: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- generic raw provider-result conversion: `EXACT_NATIVE`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- live apply provider/status schema: `UNKNOWN`;
- runtime implementation: `FENCED`.