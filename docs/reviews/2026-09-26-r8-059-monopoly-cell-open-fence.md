# R8-059 - fence monopoly_cell_open at authorization-state live-action boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained Monopoly cell-open host boundary without bypassing the native authorization-state admission or inventing game-action behavior.

## Native/frontend authority

Primary evidence:

- retained frontend API wrapper `monopoly_cell_open`;
- central retained API invoke wrapper `U()`, which injects the currently selected `profileId` when the command payload does not already contain one;
- native `monopoly_cell_open` handler `0x14017705F-0x140177AF2`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- native profile/runtime resolver `0x1402AE43C-0x1402AE4E6`;
- game-route helper `0x1403AD367-0x1403AD3B8`;
- shared game-call future `0x1400E4FAD`;
- generic JSON result converter `0x1402BB816`;
- exact game method string `openMonopolyCell`.

## Frontend/public payload

The per-command frontend wrapper calls:

`U("monopoly_cell_open")`

with no feature-specific argument object.

The shared retained `U()` wrapper normalizes missing arguments to `{}`, then injects the current selected `profileId` when available. Therefore the effective public command payload contains only the wrapper-owned profile identity; there is no Monopoly-specific user payload.

## Admission order

Native first awaits shared authorization state.

If that state is unavailable, exact failure is:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

Only after that admission succeeds does the handler resolve the injected `profileId` through the native runtime resolver.

Missing, null, non-string, or blank profile identity reaches exact:

- code: `PROFILE_ID_REQUIRED`
- message: `PROFILE_ID_REQUIRED`

A profile without a retained runtime follows the existing native runtime-unavailable path:

- code: `PROFILE_RUNTIME_UNAVAILABLE`
- message: `PROFILE_RUNTIME_UNAVAILABLE`

The game-route helper then requires connected game transport. Missing connectivity is exact:

- code: `GAME_DISCONNECTED`
- message: `game disconnected`

The authorization-state error has precedence because native awaits it before profile/runtime and route resolution.

## Live game call

After admission, native issues exactly one game call:

- method: `openMonopolyCell`
- args: `{}`
- result deadline: **5,000 ms** (`0x1388`)
- no host retry loop.

The call setup is structurally identical to the already-proven R8-046 empty-args `getSquads` path: connected-route ownership, empty request slots, shared result future, 5-second command deadline and generic correlated-result conversion.

If no result is known by the deadline, the shared future yields:

- code: `LUA_CALL_TIMEOUT`
- message: `lua call result unknown after timeout: openMonopolyCell`

A returned provider failure uses the shared `LUA_CALL_FAILED` path, including `lua call failed: <error>` when provider detail is available.

## Success result

Native does not construct a host-side Monopoly DTO. The correlated `openMonopolyCell` result passes through the shared generic JSON converter and is returned unchanged.

The exact provider-owned result fields are therefore intentionally opaque to the host contract at this checkpoint.

## Why no production implementation is added

The rebuild has a usable authenticated game-call transport, but the native command first requires owner-excluded authorization state. Adding a direct `openMonopolyCell` route that skips that admission would make the action available in states where original LWBridge returns `STATE_UNAVAILABLE`.

R8-059 therefore does not implement the action and does not synthesize an always-authorized substitute. The game/provider action itself also remains live-behavior dependent and requires later current-client validation once the retained admission can be represented without reconstructing excluded account/authentication behavior.

## Evidence classification

- shared-wrapper profile injection: `EXACT_CONTRACT`;
- authorization-state admission/order: `EXACT_NATIVE`;
- profile/runtime errors: `EXACT_NATIVE`;
- disconnect error: `EXACT_NATIVE`;
- `openMonopolyCell` method: `EXACT_NATIVE`;
- empty args: `EXACT_NATIVE`;
- 5,000 ms deadline: `EXACT_NATIVE`;
- timeout/failure mapping: `EXACT_NATIVE`;
- raw JSON success forwarding: `EXACT_NATIVE`;
- provider result schema: `UNKNOWN / provider-owned`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.
