# R8-025 — restore Alliance Garrison config persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore `alliance_garrison_config_save`, exact native config validation, `/tasks/allianceGarrison` persistence and `get_status.config.tasks` reload. Live garrison automation remains outside this checkpoint.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `SquadPanel-ClRUC9pv.js`;
- immutable API wrapper `alliance_garrison_config_save`;
- native `/tasks/allianceGarrison` config path and validator around VA `0x1403ba7e2`;
- original installed per-profile `runtime/config.json`, reading only `tasks.allianceGarrison`.

The Squad panel remains byte-identical to immutable 0.3.1. SHA-256:

`3211da6582edb72e99db942c1b5ad4a0d7b3ed831df7548e020e0eab666c03af`

R8-025 changes no frontend asset.

## Original frontend/default contract

The immutable Squad panel uses this local default before merging persisted status:

```json
{
  "enabled": false,
  "recallOnDisable": true,
  "squadPriority": [],
  "targets": []
}
```

The same object is present in the original installed profile under:

`runtime/config.json -> tasks.allianceGarrison`

The panel reloads persisted data from:

`get_status().config.tasks.allianceGarrison`

and saves the complete draft through:

`alliance_garrison_config_save(config)`.

## Persistence boundary

R8-025 stores the validated task at the exact recovered runtime-config member:

`tasks.allianceGarrison`

on the rebuild-owned per-profile runtime config introduced by the earlier config checkpoints.

Save behavior:

- strips routing-only `profileId`;
- replaces only `tasks.allianceGarrison`;
- preserves sibling tasks;
- preserves Hotkeys, visual metrics, equipment config and unknown root JSON;
- preserves opaque/future fields inside the Alliance Garrison task and target objects;
- returns the saved task object;
- `get_status.config.tasks` reads the same runtime store, so the original Squad panel reloads the saved task.

The rebuild filesystem location remains an equivalent per-profile mapping, not a claim about the original absolute path.

## Native validator contract

The native validator treats `enabled` and `recallOnDisable` as optional booleans. When `enabled` is absent it behaves as disabled for the final nonempty-target/squad guard.

`squadPriority` is required and must be an array of unique integer squad indexes from 1 through 4.

Recovered exact validation messages include:

- `alliance garrison enabled must be boolean`;
- `alliance garrison recall on disable must be boolean`;
- `alliance garrison squad priority must be an array`;
- `alliance garrison squad priority must contain integers`;
- `invalid squad index`;
- `alliance garrison squad priority contains duplicates`;
- `alliance garrison targets must be an array`;
- `alliance garrison target must be an object`;
- `invalid alliance garrison target kind`;
- `alliance garrison building id must be positive`;
- `alliance garrison ally uid is required`;
- `alliance garrison city snapshot is invalid`;
- `alliance garrison target name is too long`;
- `alliance garrison targets contain duplicates`;
- `alliance garrison requires at least one target and squad`.

These task-validation failures use native `INVALID_REQUEST`.

## Target contract

Two target kinds are accepted.

### `allianceBuilding`

Requires a positive integer `buildId`.

Its duplicate identity is:

`building:<buildId>`.

### `allyCity`

Requires a Unicode-trimmed nonblank string `uid`.

Its duplicate identity is:

`ally:<trimmed uid>`.

The UI may also persist snapshots such as `nameSnapshot`, `uuidSnapshot`, `uuidUpdatedAt`, `serverIdSnapshot` and `pointIdSnapshot`.

If `uuidSnapshot` is absent or trims to empty, server/point snapshot validation is not activated.

If a nonblank `uuidSnapshot` is present, native validation requires:

- UUID snapshot content to contain only ASCII decimal digits;
- positive integer `serverIdSnapshot`;
- positive integer `pointIdSnapshot`.

A present string `nameSnapshot` is rejected when its native character-count limit exceeds 100.

Duplicate target identities are rejected.

## Enabled-state guard

Disabled configurations may have empty `targets` and/or `squadPriority`.

When `enabled=true`, both collections must contain at least one entry. Otherwise the native error is:

`INVALID_REQUEST / alliance garrison requires at least one target and squad`.

This matches the immutable panel's own local validity gate.

## Regression coverage

`AllianceGarrisonConfigChecks` proves:

- valid save/result shape;
- `profileId` is not persisted;
- opaque top-level and target JSON is preserved;
- unrelated runtime-config root members and sibling tasks survive a save;
- disabled empty config is accepted;
- omitted optional booleans are accepted;
- boolean/type/array/integer/index/duplicate validation branches;
- both target kinds;
- blank ally UID rejection;
- strict nonblank city-snapshot validation;
- blank UUID snapshot does not require server/point snapshots;
- target-name length guard;
- duplicate building and trimmed ally identities;
- enabled target/squad nonempty guard;
- production backend routing for `alliance_garrison_config_save`;
- `get_status.config.tasks.allianceGarrison` reload;
- malformed runtime config returns `STATE_UNAVAILABLE / config state is unavailable`.

Release build and the complete deterministic suite pass with the service installed in normal production routing.

## Still intentionally missing

R8-025 does **not** implement or claim parity for:

- `automation_start("allianceGarrison")` live execution;
- `automation_stop("allianceGarrison", {recallOwned: ...})`;
- live alliance building/ally discovery providers;
- garrison assignment, march, recall or ownership logic;
- polling/runtime task-state semantics;
- any game-bridge method used by garrison execution.

Those behaviors remain provider/protocol dependent and must not be inferred from the persisted config contract.

## Validation

Completed before packaging:

- Squad panel hash matches immutable 0.3.1;
- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
