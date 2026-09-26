# R8-050 - fence city_layout_snapshot_get at authorization-state boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the observable host-side boundary of the retained read-only City Layout snapshot command while refusing to recreate its owner-excluded authorization-state-derived request input.

## Native authority

Primary evidence:

- `city_layout_snapshot_get` handler `0x140198AA5-0x140199316`;
- authorization-state future `0x1400DC79E-0x1400DC995`;
- City Layout game helper `0x1400DC30C-0x1400DC58F`;
- selected-profile runtime resolver `0x1402AE43C`;
- shared game-call future `0x1400E4FAD`;
- generic JSON result converter `0x1402BB816`;
- retained frontend wrapper `city_layout_snapshot_get` in `api-ClPPi2JT.js`.

## Authorization-state dependency

The public frontend invokes `city_layout_snapshot_get` with no payload. Native does not immediately call the game. The handler first awaits the shared authorization-state future.

If that state is unavailable, the exact error is:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

The resulting authorization-state value is carried forward as the argument supplied to the City Layout game-call helper. Its exact shape/source is therefore account/auth-state dependent.

The owner explicitly excludes authentication/auth-state/account-purpose recovery. R8-050 does not decode, infer, substitute, or synthesize that argument.

## Game-call boundary

After the authorization-state dependency is satisfied, the native City Layout helper resolves the selected profile runtime and verifies the connected game route.

Missing connectivity returns exact:

- code: `GAME_DISCONNECTED`
- message: `game disconnected`

When connected, native prepares exactly one call to:

- method: `getCityLayoutSnapshot`
- result deadline: **10,000 ms** (`0x2710`)
- no handler retry loop.

The request argument is the authorization-state-derived value described above and is intentionally unclaimed.

## Success/result handling

After the correlated game result completes, the command passes the value through the shared generic JSON converter (`0x1402BB816`). The handler does not construct a separate host-side snapshot DTO.

Therefore the recoverable public success rule is: return the provider JSON value unchanged. Exact provider fields remain provider-owned and cannot be safely fabricated without the excluded request input/provider execution.

## Why no production implementation is added

The rebuild already has City Layout draft persistence from R8-018, but the live snapshot/validate/apply provider family is not implemented. Creating a host-only snapshot request with `{}`, a profile ID, or frontend-derived fields would not match the original command because native passes an authorization-state-derived argument.

R8-050 therefore makes no runtime-code change. `city_layout_snapshot_get` remains explicitly fenced at the authorization-state boundary; validate/apply-start/status/cancel remain separate protected/provider checkpoints.

## Evidence classification

- authorization unavailable error: `EXACT_NATIVE`;
- connected method name: `EXACT_NATIVE`;
- 10,000 ms deadline: `EXACT_NATIVE`;
- disconnected error: `EXACT_NATIVE`;
- raw correlated JSON forwarding: `EXACT_NATIVE`;
- authorization-derived request argument: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.