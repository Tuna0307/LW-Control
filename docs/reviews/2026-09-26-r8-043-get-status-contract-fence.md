# R8-043 — fence incomplete get_status projection

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the native public `get_status` top-level contract and remove any claim that the current rebuild projection is one-for-one while the native config normalizer remains incomplete.

## Authority

Primary native evidence:

- `get_status` handler `0x140152388-0x140152C2E`;
- selected-profile runtime resolver `0x1402AE43C`;
- native status producer `0x1403CEA9A-0x1403CEE28`;
- public status serializer `0x1403D204E-0x1403D21F6`;
- runtime config loader `0x1403CCC76-0x1403CD4A1`;
- config migration/normalization helper `0x1403AD60C-0x1403B4FCA`;
- Unix-millisecond clock helper `0x14023F1C0-0x14023F244`;
- retained frontend API wrapper `get_status` and status/config consumers.

R8-043 stays outside the protected package-key RVA lane. No live authentication/account probing is used.

## Exact public top-level schema

The native serializer emits exactly seven top-level fields in this order:

1. `ok`
2. `backend`
3. `runtimeRoot`
4. `pending`
5. `xluaOnline`
6. `lastXluaActivity`
7. `config`

Recovered scalar ownership:

- `ok`: boolean; successful native status production writes `true`.
- `backend`: string; native chooses `offline` or `named-pipe` from exact route presence.
- `runtimeRoot`: string derived from the selected profile runtime root.
- `pending`: scalar integer copied from the native bridge state pending counter.
- `xluaOnline`: boolean; it is the same route-presence decision that selects `backend="named-pipe"`.
- `lastXluaActivity`: scalar Unix-millisecond timestamp. The producer returns the maximum of the stored bridge-state activity value and the selected route's recovered activity timestamps; zero is retained when no activity exists.
- `config`: generic JSON value produced by the native runtime-config loader/normalizer.
## Native state/error behavior

The native producer first acquires the selected runtime bridge state. If that state is unavailable it returns `STATE_UNAVAILABLE`; the exact recovered message is `bridge state is unavailable`.

The handler uses the same selected-profile runtime resolver recovered for R8-040/R8-042. The stable runtime-resolution codes remain `PROFILE_ID_REQUIRED` and `PROFILE_RUNTIME_UNAVAILABLE`; exact prose for those two code-centric paths remains unclaimed.

`backend`/`xluaOnline` are transport-route state, not the rebuild's Overview heartbeat-readiness projection. A disconnected/no-route runtime therefore reports `backend="offline"`, `xluaOnline=false`; a present named-pipe route reports `backend="named-pipe"`, `xluaOnline=true`.

## Config boundary

The `config` field is not a small fixed DTO. Native resolves `<runtimeRoot>\config.json`, requires an object-shaped configuration, applies a large migration/normalization function, and then places the resulting JSON value into the status object.

The missing-file/default path is partially recovered: it seeds a legacy-compatible object containing `enable_eval=false`, `auto_shield=true`, and `auto_red_packet_treasure=true` before entering the native normalizer. The normalizer then traverses dozens of paths spanning task automation, scheduler timings, chat automation, trade station, hotkeys, visual metrics, alliance/train/railway settings, equipment, stamina and related retained configuration.

Malformed/non-object config reaches the native `INVALID_CONFIG` family. The complete one-for-one normalization/default table is not yet closed, so R8-043 does not substitute a guessed config projection.

## Rebuild deviation fenced by this checkpoint

The current rebuild `CreateStatus()` is not native parity. It currently exposes only `xluaOnline`, `pending`, and a hand-built `config` subset. Specifically:

- it omits native `ok`, `backend`, `runtimeRoot`, and `lastXluaActivity`;
- its `pending` can be null when the local RPC registry is absent, while native status serializes a scalar counter after runtime-state admission;
- its `xluaOnline` is tied to retained Overview readiness/heartbeat rather than native named-pipe route presence;
- its config object hard-codes four automation flags plus tasks and therefore omits broader native config/migration behavior;
- it still uses the generic rebuild optional-profile scope gate rather than the native selected-profile runtime error path.

Because `config` is directly consumed by multiple retained frontend panels, replacing the current object with an only-partially recovered top-level projection would still be a parity claim the evidence does not support. R8-043 therefore records the exact boundary and leaves implementation unchanged until the config normalizer can be recovered one-for-one.
## Frontend relevance

The retained frontend consumes `status.pending`, `status.xluaOnline`, and multiple branches beneath `status.config`. Direct config consumers include the shield/reconnect/popup flags, automation tasks, chat automation, trade station, monster sweep, stamina potion and alliance garrison views. The frontend's compatibility fallbacks for `auto_shield` are consistent with the native missing-file legacy seed.

## Validation / next evidence

This checkpoint is research/fencing only; no runtime behavior is changed. Full repository build/test/frontend/diff gates are still required before committing the documentation/evidence checkpoint.

To implement `get_status` one-for-one, the remaining required evidence is the complete observable output of `0x1403AD60C-0x1403B4FCA`: defaults, migrations, validation failures and preservation rules for every retained config path. Do not replace that with raw-file passthrough or a frontend-only subset.
