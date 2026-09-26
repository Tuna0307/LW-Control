# R8-047 - restore monster_catalog_options

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the retained read-only `monster_catalog_options` command as the native private game call, without host-side option rewriting.

## Native authority

Primary evidence:

- handler `0x14019676B-0x140197132`;
- selected-profile runtime resolver `0x1402AE43C`;
- shared game-call future `0x1400E4FAD-0x1400E537E`;
- generic JSON result converter `0x1402BB816`;
- retained frontend API wrapper `monster_catalog_options` in `api-ClPPi2JT.js`;
- retained Squads/AFK consumer in `SquadPanel-ClRUC9pv.js`.

## Public contract

`monster_catalog_options` uses the same selected-profile runtime admission already recovered for `squad_list`: missing/null/non-string/blank `profileId` fails with exact `PROFILE_ID_REQUIRED` / `PROFILE_ID_REQUIRED`; an unknown runtime fails with exact `PROFILE_RUNTIME_UNAVAILABLE` / `PROFILE_RUNTIME_UNAVAILABLE`.

Without an active game route native returns exact `GAME_DISCONNECTED` / `game disconnected`.

With a connected route native issues exactly one game request:

- method: `getMonsterCatalogOptions`
- args: `{}`
- result deadline: 5,000 ms (`0x1388`)
- no host retry loop.

Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getMonsterCatalogOptions`. Game-side failures continue through the shared `LUA_CALL_FAILED` mapping.

## Success result

After correlation, native runs the shared generic JSON result converter and returns that JSON value. There is no host-owned DTO normalization in the handler.

The retained frontend reads `result.options` and consumes fields including `key`, `group`, `monsterNameKey`, `monsterType`, `monsterSpecial`, `monsterIds`, `searchable`, `minLevel`, `maxLevel`, `attackMinLevel`, and `attackMaxLevel`. Those fields describe the live game-provider payload; R8-047 intentionally forwards the provider JSON unchanged and preserves unknown/opaque fields.

## Rebuild implementation

The command uses the authenticated retained bridge transport through a private backend path. Public generic `call_lua` remains restricted to its prior recovered `getStatus` boundary.

R8-046 already added command-specific result deadlines to the equivalent .NET transport. R8-047 reuses that facility with the native 5,000 ms deadline and command-specific timeout message.

Observable command behavior is classified `EXACT_NATIVE`; .NET task/registry framing remains `EQUIVALENT_REIMPLEMENTATION` plumbing.

## Deterministic coverage

An authenticated fake proxy proves that:

- `monster_catalog_options` emits a correlated `getMonsterCatalogOptions` request with exact `{}` args;
- a success payload containing `options` plus an opaque top-level field is forwarded unchanged;
- an unanswered second request remains pending until the native 5,000 ms deadline and fails with exact timeout code/message;
- valid profile with no game route returns exact `GAME_DISCONNECTED` / `game disconnected`;
- public generic `call_lua` is not widened.

## Remaining boundary

The live game/provider implementation that constructs monster catalog options remains provider-owned; the host does not reconstruct it. Live current-client compatibility still needs validation, and live Monster AFK execution remains a separate provider/action boundary.