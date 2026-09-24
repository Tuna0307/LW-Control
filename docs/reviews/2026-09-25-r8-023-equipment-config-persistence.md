# R8-023 — restore Equipment configuration persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the offline/profile-scoped `equipment_config_get/save` contract and native runtime-config merge behavior without implementing equipment application/game actions.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `SquadPanel-ClRUC9pv.js`;
- immutable API wrapper definitions in `api-ClPPi2JT.js`;
- native `equipment_config_get` handler path near VA `0x1401875ae`;
- native `equipment_config_save` handler path near VA `0x140129d16`;
- native equipment projection helper near VA `0x1403b683e`;
- native equipment preset validation/merge helper near VA `0x1403b85a5`;
- native runtime-config equipment merge path near VA `0x1403cbcd3`;
- observed original profile runtime config under `%APPDATA%\lwbridge\profiles\<profile>\runtime\config.json`, SHA-256 `4f7981d85da6703174eb8daeea1c4658aac262c920f0f4cbdddbfce75c01fa55`.

The original and rebuild Squad panel chunks are byte-identical:

`3211da6582edb72e99db942c1b5ad4a0d7b3ed831df7548e020e0eab666c03af`

R8-023 changes no frontend asset.

## Original storage boundary

The original profile runtime config contains `equipmentPresets` as a top-level member. The observed reference profile has:

```json
{
  "equipmentPresets": []
}
```

and no `initialEquipmentConfig` member in the uncaptured/default state.

The original binary also contains legacy config members:

- `equipmentSchemes`;
- `squadEquipmentBindings`.

The native save merge removes those legacy members while writing the current equipment representation.

The rebuild reuses the same per-profile runtime-config abstraction already used for Hotkeys and visual metrics. It preserves unknown sibling JSON instead of replacing the whole config document.

## `equipment_config_get`

The native get handler reads the shared runtime config and passes it through the recovered equipment projection helper.

Recovered projection rules:

- return an object;
- `equipmentPresets` is always present;
- missing `equipmentPresets` defaults to `[]`;
- existing `equipmentPresets` is copied without narrowing nested JSON;
- `initialEquipmentConfig` is copied only when the member exists;
- missing `initialEquipmentConfig` is omitted, not synthesized as `null` or an empty object.

R8-023 reproduces those rules.

## `equipment_config_save` validation

The native save path requires `equipmentPresets` to exist and be an array.

If the member is missing or not an array:

- code: `INVALID_REQUEST`;
- message: `invalid equipment presets`.

Every preset array item then passes a second recovered validator. Each item must:

- be an object;
- contain string `id`;
- contain string `name`;
- have a non-empty Unicode-whitespace-trimmed `id`;
- have a non-empty Unicode-whitespace-trimmed `name`;
- have a trimmed `id` unique within the array.

Failure uses:

- code: `INVALID_REQUEST`;
- message: `invalid equipment preset`.

The validator does not narrow or rewrite the remaining nested preset JSON at this boundary. R8-023 therefore preserves unknown/future nested preset members rather than inventing an equipment schema.

## Save/merge semantics

After validation, the native save path merges the equipment projection into the current runtime config.

Recovered behavior reproduced in R8-023:

- replace `equipmentPresets` with the supplied array;
- if `initialEquipmentConfig` is supplied, store it as provided;
- if `initialEquipmentConfig` is omitted, remove the stored member;
- remove legacy `equipmentSchemes`;
- remove legacy `squadEquipmentBindings`;
- preserve unrelated/unknown sibling runtime-config members;
- return the newly projected equipment config.

The shared config-state failure vocabulary remains:

- code: `STATE_UNAVAILABLE`;
- message: `config state is unavailable`.

## Production routing

R8-023 adds `EquipmentConfigCommandService` to the production composite command router using the same profile runtime-config path as Hotkeys and visual metrics.

Implemented:

- `equipment_config_get`;
- `equipment_config_save`.

Intentionally still fenced:

- `equipment_preset_apply`;
- native `equipment_initial_apply`.

Those commands cross into game-action execution and are not implied by config persistence.

The immutable API bundle exposes the original `equipment_preset_apply` wrapper. `equipment_initial_apply` is present in the native command inventory even though no matching literal occurs in the recovered frontend chunks.

## Regression coverage

`EquipmentConfigChecks` proves:

- missing config returns exactly `equipmentPresets: []` with no synthesized initial config;
- save persists presets and optional initial capture;
- arbitrary nested preset JSON is preserved;
- unknown sibling runtime config is preserved;
- Hotkey and visual-metrics sibling state is preserved;
- legacy `equipmentSchemes` is removed on save;
- legacy `squadEquipmentBindings` is removed on save;
- reopen/get returns persisted equipment state;
- omitting `initialEquipmentConfig` removes the stored member;
- missing/non-array `equipmentPresets` uses exact native error vocabulary;
- malformed/blank/duplicate-trimmed preset identities use exact native error vocabulary;
- corrupt runtime config returns `STATE_UNAVAILABLE / config state is unavailable`;
- production backend routes `equipment_config_get`;
- both equipment apply commands remain `COMMAND_NOT_IMPLEMENTED`.

The complete deterministic suite remains green.

## Still outside this checkpoint

R8-023 does not claim parity for:

- equipment catalog acquisition;
- live squad/hero/equipment discovery;
- `equipment_preset_apply`;
- `equipment_initial_apply`;
- operation/progress events;
- partial-apply semantics;
- duplicate equipment UUID runtime handling;
- no-binding behavior;
- per-hero equipment requests;
- game-side rollback or recovery behavior.

Native evidence for those action paths exists, but they are separate from the offline config store and remain protected until the provider/protocol boundary is recovered.

## Validation

Completed before packaging:

- Squad panel chunk: exact immutable 0.3.1 hash;
- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed in this checkpoint.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
