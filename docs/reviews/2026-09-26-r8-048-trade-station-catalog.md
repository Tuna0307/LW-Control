# R8-048 - restore trade_station_catalog

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the retained read-only Trade Station catalog command without enabling Trade Station mutation/provider execution.

## Native authority

Primary evidence:

- `trade_station_catalog` handler `0x14012B557-0x14012BFEA`;
- selected-profile runtime resolver `0x1402AE43C`;
- game-route helper `0x1403AD367-0x1403AD3B8`;
- shared game-call future `0x1400E4FAD-0x1400E537E`;
- generic JSON result converter `0x1402BB816`;
- retained frontend API wrapper and Automation Trade Station consumer.

## Public admission and errors

Native uses the same selected-profile runtime admission recovered in R8-046/047. Missing/null/non-string/blank `profileId` returns exact `PROFILE_ID_REQUIRED` / `PROFILE_ID_REQUIRED`. An unresolved profile runtime returns exact `PROFILE_RUNTIME_UNAVAILABLE` / `PROFILE_RUNTIME_UNAVAILABLE`.

The shared route helper explicitly returns exact `GAME_DISCONNECTED` / `game disconnected` when the selected game route is unavailable.

## Game-call contract

After route admission native issues exactly one game call:

- method: `getTradeStationCatalog`
- args: `{}`
- result deadline: **10,000 ms** (`0x2710`)
- no host retry loop.

That deadline is intentionally different from the 5,000 ms deadlines recovered for `getSquads` and `getMonsterCatalogOptions`.

Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getTradeStationCatalog`. Returned game failures use the shared `LUA_CALL_FAILED` path.

## Success result

Native converts the correlated game result through the shared generic JSON converter and returns that value. The handler does not construct a host-owned Trade Station catalog DTO.

The retained frontend reads `result.items` and then renders/configures the returned goods. R8-048 therefore preserves the provider JSON unchanged, including unknown/opaque fields, instead of imposing a rebuild schema.

## Rebuild implementation

R8-048 adds a private `trade_station_catalog` backend path over the authenticated retained bridge transport. It reuses the per-call deadline facility restored in R8-046, supplying the native 10-second deadline and command-specific timeout message.

Public generic `call_lua` remains restricted to its prior `getStatus` boundary. `trade_station_configure` remains fenced and is not enabled by this checkpoint.

Observable command behavior is classified `EXACT_NATIVE`; .NET task/registry transport mechanics remain `EQUIVALENT_REIMPLEMENTATION`.

## Deterministic coverage

An authenticated fake proxy proves:

- outbound correlation reaches `getTradeStationCatalog` with exact `{}` args;
- successful `items` JSON plus an opaque field is forwarded unchanged;
- an unanswered second call remains pending until the recovered 10-second deadline;
- timeout code/message are exact;
- missing profile, unknown profile runtime and disconnected route use exact native error vocabulary;
- generic public `call_lua` remains fail-closed.

## Remaining boundary

The live game/provider implementation that produces catalog items remains provider-owned. Trade Station configuration/execution is still fenced pending its own complete provider/result evidence. Live current-client compatibility remains to be validated.