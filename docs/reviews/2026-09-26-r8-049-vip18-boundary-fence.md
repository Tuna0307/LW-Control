# R8-049 - fence VIP18 base family at authorization/config-state boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** record the recoverable public/cache behavior of the VIP18 base read paths while stopping before excluded authorization/account-state internals or unrecovered config migration.

## vip18_base_config_get

Native resolves the selected profile runtime and reads the shared native config-state object through helper `0x1403C57F7`. If that config state is unavailable the exact error is:

- code: `STATE_UNAVAILABLE`
- message: `config state is unavailable`

The projector helper `0x1403B5192` builds exactly the retained frontend config fields:

- `selectedSkinId`
- `autoApplyOnStart`
- `favoriteSkinIds`

The normalization/default source is the same shared native config-state/migration layer that remains incomplete under R8-043. The frontend's local default object is not sufficient authority to recreate the native persistence/migration contract. No runtime implementation is added.

## vip18_base_list

The handler parses optional `refresh` as a boolean; missing or non-boolean values project as false. It resolves the selected profile runtime, but before constructing the catalog-cache identity it also awaits native authorization state through `0x1400DC58F`. That helper has exact unavailable behavior:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

Authorization/account-state recovery is explicitly outside retained owner scope, so the dynamic cache identity carried through that authorization-state-dependent path is intentionally not decoded or recreated.

Observable behavior closed without crossing that boundary:

- public success is an object containing `items`; cache metadata is not exposed;
- native persistent cache filename format begins `base-skin-catalog-` and ends `.json`, with one dynamic identity component carried across the authorization-state-dependent path; its exact source is intentionally unclaimed;
- cache record contains `version`, `updatedAt`, and `items`; native writes `version = 1` and current Unix-ms `updatedAt` on successful refresh;
- missing/unreadable/unparseable cached content falls back toward an empty `items` projection rather than requiring game connectivity;
- `refresh=false` does not issue the game request;
- `refresh=true` checks live game connectivity; if no route is connected it falls back to cached items rather than raising `GAME_DISCONNECTED`;
- when refresh is requested and a route exists, native issues `getVip18BaseSkins` with a 10,000 ms deadline and no handler retry loop;
- refreshed provider JSON is normalized to its `items` field and persisted in the cache record before the public `items` result is returned.

## Why this is fenced

Closing the cache suffix source would require following the authorization-state-dependent identity path further, which conflicts with the owner's explicit exclusion of auth-state/account-purpose behavior. Closing `vip18_base_config_get` would require the still-unrecovered shared config migration/normalizer. Neither dependency should be guessed from frontend defaults or nearby strings.

R8-049 therefore makes no production-code change. The VIP18 base family remains fenced, and work should continue on retained non-auth commands.