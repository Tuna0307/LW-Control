# R8-061 - fence vip18_base_config_save at authorization/config-state boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained VIP18 base-config save validation/persistence/result boundary without reconstructing owner-excluded authorization state or the still-incomplete shared config-state migration/normalizer.

## Native/frontend authority

Primary evidence:

- retained frontend wrapper `vip18_base_config_save(config)`;
- shared frontend invoke wrapper `U()`, which adds the selected `profileId` when absent;
- native handler `0x14014C5B2-0x14014CE40`;
- authorization-state future `0x1400DC58F-0x1400DC79E`;
- profile/runtime resolver `0x1402AE43C-0x1402AE4E6`;
- VIP18 save/validation helper `0x1403B778B-0x1403B7AA8`;
- shared config-state writer `0x1403CB790-0x1403CBCD3`;
- public VIP18 config projector `0x1403B5192-0x1403B56C2`;
- R8-049 VIP18 read-family boundary evidence.

## Public payload and admission

The frontend passes the config object directly to `U("vip18_base_config_save", config)`. Shared `U()` injects the selected `profileId` unless the object already contains one.

Native awaits authorization state before resolving the selected profile runtime.

Unavailable authorization state is exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

After that admission, native resolves the wrapper-supplied profile identity through the common runtime resolver, preserving the existing exact profile/runtime failures:

- `PROFILE_ID_REQUIRED`
- `PROFILE_RUNTIME_UNAVAILABLE`

## Native config validation

The dedicated save helper reads the retained fields:

- `selectedSkinId`
- `autoApplyOnStart`
- `favoriteSkinIds`

### selectedSkinId

The helper accepts the no-selection branch and, when a numeric skin ID is supplied, requires a positive integer-like value.

Invalid supplied skin ID uses exact:

- code: `INVALID_REQUEST`
- message: `invalid base skin id`

R8-061 does not overclaim every JSON-number representation/null coercion edge beyond the recovered positive-ID/no-selection branches.

### autoApplyOnStart

The helper requires a boolean value for `autoApplyOnStart`.

Missing or non-boolean/invalid value reaches exact:

- code: `INVALID_REQUEST`
- message: `invalid base skin auto-apply setting`

### favoriteSkinIds

The field is passed through a dedicated native sequence normalizer before persistence.

The executable exposes no separate favorites-specific public error string in this save lane. The exact per-element coercion/drop/duplicate semantics of that sequence helper are not fully closed in R8-061, so they remain explicitly unknown rather than inferred from frontend defaults.

## Config-state persistence

After validation, native updates the shared config-state owner.

Recovered exact persistence ownership includes:

- top-level config family: `vip18_profiles`;
- saved fields: `selectedSkinId`, `autoApplyOnStart`, `favoriteSkinIds`;
- persisted file family: `config.json`.

The shared writer first requires live config state. If unavailable, exact error is:

- code: `STATE_UNAVAILABLE`
- message: `config state is unavailable`

This is the same shared native config-state/migration lane that blocks strict `vip18_base_config_get` in R8-049 and the full `get_status.config` projection in R8-043.

The exact whole-file migration/default/merge semantics remain incomplete and are not replaced with a frontend-derived config object.

## Success result

After successful persistence, native returns the public VIP18 config through projector `0x1403B5192`.

The public result contains the same three retained fields as config-get:

- `selectedSkinId`
- `autoApplyOnStart`
- `favoriteSkinIds`

The result is therefore the normalized persisted config projection, not a generic `{ok:true}` acknowledgment.

## Why no production implementation is added

There are two independent native dependencies that must not be guessed:

1. authorization-state admission is owner-excluded;
2. persistence belongs to the still-unrecovered shared `config.json` migration/normalization state owner.

Implementing only a local three-field JSON file or using frontend defaults would diverge from both native admission and native config ownership.

R8-061 therefore makes no runtime-code change.

## Evidence classification

- frontend payload/profile injection: `EXACT_CONTRACT`;
- authorization-state admission: `EXACT_NATIVE`;
- profile/runtime resolver errors: `EXACT_NATIVE`;
- retained field names: `EXACT_NATIVE`;
- invalid skin-ID error: `EXACT_NATIVE`;
- invalid auto-apply error: `EXACT_NATIVE`;
- favorites sequence ownership: `EXACT_NATIVE`;
- favorites element-normalization details: `UNKNOWN`;
- `vip18_profiles` / `config.json` persistence ownership: `EXACT_NATIVE`;
- config-state unavailable error: `EXACT_NATIVE`;
- three-field success projection: `EXACT_NATIVE`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- shared config migration/normalizer: `UNKNOWN`;
- runtime implementation: `FENCED`.
