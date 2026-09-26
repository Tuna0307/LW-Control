# R8-060 - fence construction_rewards_claim at authorization-state live-action boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained Construction Rewards claim host boundary without bypassing native authorization-state admission or inventing live claim behavior.

## Native/frontend authority

Primary evidence:

- retained frontend wrapper `construction_rewards_claim`, located in the Automation API block beside configure/start/stop and trade-station helpers;
- shared frontend invoke wrapper `U()`, which normalizes missing args to `{}` and injects the current selected `profileId`;
- native handler `0x140179BC9-0x14017A65C`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- profile/runtime resolver `0x1402AE43C-0x1402AE4E6`;
- connected-route helper `0x1403AD367-0x1403AD3B8`;
- shared game-call future `0x1400E4FAD`;
- generic JSON result converter `0x1402BB816`;
- exact provider method `claimConstructionRewards`.

## Public payload

The feature wrapper invokes:

`U("construction_rewards_claim")`

with no claim-specific object.

Shared `U()` converts that to an object and injects selected `profileId` when available. There are no Construction Rewards-specific request fields in the frontend wrapper or native handler before the live call.

## Admission order

Native awaits shared authorization state first.

Unavailable authorization state returns exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

After that succeeds, the injected profile identity is resolved through the common runtime resolver.

Missing/blank/non-string profile identity reaches:

- code: `PROFILE_ID_REQUIRED`
- message: `PROFILE_ID_REQUIRED`

A profile without a retained runtime follows:

- code: `PROFILE_RUNTIME_UNAVAILABLE`
- message: `PROFILE_RUNTIME_UNAVAILABLE`

Native next requires a connected game route:

- code: `GAME_DISCONNECTED`
- message: `game disconnected`

The authorization-state failure has precedence because it is awaited before profile/runtime and route resolution.

## Live provider call

Native performs one game request:

- method: `claimConstructionRewards`
- args: `{}`
- result deadline: **5,000 ms** (`0x1388`)
- no host retry loop.

The call structure matches the already recovered shared empty-args provider pattern used by R8-046 and R8-059.

If no correlated result is known by the deadline, native uses the shared result future:

- code: `LUA_CALL_TIMEOUT`
- message: `lua call result unknown after timeout: claimConstructionRewards`

Provider errors use the shared `LUA_CALL_FAILED` path, including `lua call failed: <error>` when detail is supplied.

## Success result

Native forwards the correlated provider result through the generic JSON converter.

There is no host-side Construction Rewards DTO rewrite in this handler. Provider-owned success fields therefore pass through unchanged and remain opaque to the host contract.

## Why no production implementation is added

The rebuild can issue authenticated bridge calls, but original LWBridge first requires owner-excluded authorization-state admission. Wiring `claimConstructionRewards` directly to the game route would enable a live claim action in states where native returns `STATE_UNAVAILABLE`.

R8-060 therefore keeps the action fenced and does not synthesize an always-authorized substitute.

## Evidence classification

- shared-wrapper selected-profile injection: `EXACT_CONTRACT`;
- authorization-state admission/order: `EXACT_NATIVE`;
- profile/runtime errors: `EXACT_NATIVE`;
- disconnect error: `EXACT_NATIVE`;
- provider method `claimConstructionRewards`: `EXACT_NATIVE`;
- empty args: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- timeout/failure mapping: `EXACT_NATIVE`;
- raw JSON success forwarding: `EXACT_NATIVE`;
- provider result schema: `UNKNOWN / provider-owned`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.
