# R8-024 — restore Monster AFK configuration persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore profile-scoped `monster_afk_config_save`, exact recovered validation/error behavior, shared runtime-config persistence at `/tasks/monsterSweep`, and `get_status.config.tasks` read-back. Monster AFK start/stop game execution remains fenced.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `SquadPanel-ClRUC9pv.js`;
- immutable API wrapper `monster_afk_config_save` in `api-ClPPi2JT.js`;
- original per-profile runtime config under `%APPDATA%\lwbridge\profiles\<profile>\runtime\config.json`;
- native `monster_afk_config_save` command path near VA `0x140126348`;
- native Monster AFK validator at VA `0x1403bc1fd`;
- native `/tasks/monsterSweep` patch-path xref at VA `0x14012689d`;
- native shared-config failure path using `STATE_UNAVAILABLE / config state is unavailable`.

The Squad panel remains byte-identical to the immutable 0.3.1 chunk:

`3211da6582edb72e99db942c1b5ad4a0d7b3ed831df7548e020e0eab666c03af`

R8-024 changes no frontend asset.

## Frontend contract

The original wrapper sends the Monster AFK object directly at command top level and adds the active profile routing field:

```text
monster_afk_config_save({ ...config, profileId })
```

The original Squad panel uses the command as a state-store write function:

```text
write: async draft => normalize(await monster_afk_config_save(draft, profile))
```

The read path is:

```text
get_status(profile).config.tasks.monsterSweep
```

Therefore a persistence implementation is incomplete unless the saved task is also exposed through `get_status.config.tasks`. R8-024 restores both sides of that loop.

## Native storage path

The verified original runtime config contains Monster AFK under:

```text
tasks.monsterSweep
```

The native save handler references the exact JSON patch path:

```text
/tasks/monsterSweep
```

The observed untouched original runtime config has SHA-256:

`4f7981d85da6703174eb8daeea1c4658aac262c920f0f4cbdddbfce75c01fa55`

and contains a current-format Monster AFK object with:

- top-level `enabled`;
- `strategies`;
- `allianceDrill.enabled`;
- `allianceDrill.activeRally`;
- `allianceDrill.squadIndexes`.

The save path patches only the Monster AFK task and preserves sibling task/config JSON.

## Exact top-level validation

The native validator requires the submitted task config to be an object containing:

- `strategies` as an array;
- `enabled` as a boolean;
- `allianceDrill` as an object.

Malformed top-level state returns:

- code: `INVALID_REQUEST`;
- message: `invalid monster AFK config`.

## Alliance Drill validation

`allianceDrill` requires:

- boolean `enabled`;
- boolean `activeRally`;
- array `squadIndexes`.

Each squad index must be an integer from **1 through 4**. Any other value returns:

- `INVALID_REQUEST / invalid squad index`.

Duplicate drill squad indexes return:

- `INVALID_REQUEST / duplicate alliance drill squad`.

When drill `enabled=true`, the squad array must be non-empty:

- `INVALID_REQUEST / alliance drill squad required`.

Malformed drill structure returns:

- `INVALID_REQUEST / invalid alliance drill config`.

## Strategy validation

Each strategy is validated independently.

### Identity

`id` must be a nonblank string after native Unicode whitespace trimming. IDs must be unique after trimming.

Failures return:

- `INVALID_REQUEST / invalid monster AFK strategy id`.

### Kind and action state

`kind` is exactly one of:

- `farm`;
- `join`.

Anything else returns:

- `INVALID_REQUEST / invalid monster AFK strategy kind`.

The native validator derives the expected action booleans from `kind`:

- `farm` => `attackEnabled=true`, `joinEnabled=false`;
- `join` => `attackEnabled=false`, `joinEnabled=true`.

For `join`, `rally` must additionally be boolean true.

Any mismatch returns:

- `INVALID_REQUEST / invalid monster AFK strategy action`.

Other strategy metadata such as target/name/catalog fields is preserved as opaque JSON by this persistence boundary.

### Execution limit

If `executionLimit` is present, it must be a nonnegative integer.

Failure:

- `INVALID_REQUEST / invalid monster AFK execution limit`.

### Level range

The native level-range branch is active only when `levelFilterEnabled` is boolean true.

Then:

- `minLevel` must resolve to a positive integer;
- if `progressiveLevels` is not boolean true, `maxLevel >= minLevel`;
- boolean `progressiveLevels=true` relaxes the max/min ordering requirement.

Failure:

- `INVALID_REQUEST / invalid monster AFK level range`.

### Enabled state and squads

Each strategy requires boolean `enabled`.

Malformed/missing enabled state returns:

- `INVALID_REQUEST / invalid monster AFK strategy state`.

`squadIndexes` must be an array of unique integer squad indexes from 1 through 4.

Failures:

- out of range/non-integer: `INVALID_REQUEST / invalid squad index`;
- duplicate: `INVALID_REQUEST / duplicate monster AFK squad`;
- missing/wrong-type array: `INVALID_REQUEST / monster AFK squad required`;
- empty array while strategy `enabled=true`: `INVALID_REQUEST / monster AFK squad required`.

A disabled strategy may retain an empty squad array.

## Persistence/read-back behavior restored

R8-024 uses the same rebuild-owned per-profile runtime-config file already shared by Hotkeys, visual metrics, and equipment.

Successful save:

1. validates the submitted Monster AFK object;
2. removes routing-only `profileId` from persisted task JSON;
3. patches `tasks.monsterSweep`;
4. preserves unrelated root JSON and sibling tasks;
5. returns the saved config-shaped object;
6. exposes persisted `tasks` through `get_status.config.tasks`, allowing the original Squad panel read function to reload `monsterSweep`.

Malformed runtime config maps through the recovered shared config-state failure:

- `STATE_UNAVAILABLE / config state is unavailable`.

## Explicit runtime fence

R8-024 does **not** implement:

- `monster_afk_start`;
- `monster_afk_stop`;
- squad worker execution;
- map target acquisition;
- attack/rally dispatch;
- march ownership;
- stamina consumption;
- automatic retry/progression;
- game-side status/event timing.

The two runtime action commands remain `COMMAND_NOT_IMPLEMENTED` in the rebuild until the native game/provider protocol is evidence-backed.

## Regression coverage

`MonsterAfkConfigChecks` proves:

- save result is config-shaped;
- routing-only `profileId` is not persisted;
- unknown task/strategy JSON survives;
- root siblings and sibling tasks survive;
- `get_status.config.tasks.monsterSweep` reloads the saved config;
- disabled strategies may have no squads;
- progressive-level mode permits max below min;
- exact top-level/drill validation errors;
- squad range is exactly 1..4;
- duplicate drill/strategy squads fail with native messages;
- blank/duplicate strategy IDs fail;
- invalid kind/action/rally state fails;
- negative execution limit fails;
- invalid level range fails;
- missing strategy enabled state fails;
- malformed runtime state returns `STATE_UNAVAILABLE`;
- `monster_afk_start/stop` remain fenced.

## Validation

Completed during implementation:

- Release build: 0 warnings / 0 errors;
- full deterministic suite after save/read-back integration: `ok=true`, `failures=[]`, exit code 0;
- immutable/current Squad panel hashes match;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
