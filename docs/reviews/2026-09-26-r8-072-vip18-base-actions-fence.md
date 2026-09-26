# R8-072 — fence VIP18 Base Apply / Restore at authorization + shared-config boundaries

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1

R8-072 closes the retained host/provider/config-mutation contract for `vip18_base_apply` and `vip18_base_restore` without reconstructing owner-excluded authorization state or the still-incomplete shared `config.json` state owner.

## Exact frontend payloads

The immutable API wrapper sends:

- Apply: `vip18_base_apply({ skinId })`; the shared wrapper injects selected `profileId`.
- Restore: `vip18_base_restore({ profileId? })`; the shared wrapper injects selected `profileId` when absent.

## Exact native handlers

- Apply: `0x14013AB5F-0x14013BBE7`
- Restore: `0x14016A7C6-0x14016B69A`

Both first await the same VIP18 authorization-state future used by the already-audited family, then resolve the profile runtime and require a usable game route.

Recovered shared failures include:

- `STATE_UNAVAILABLE / authorization state is unavailable`
- common profile/runtime resolver failures
- `GAME_DISCONNECTED / game disconnected`

## Apply

Apply reads `skinId` and requires a positive integer-like value. Invalid input uses exact:

- code: `INVALID_REQUEST`
- message: `invalid base skin id`

When admitted and connected, native sends one provider request to:

- method: `setLocalVip18BaseSkin`
- arguments: object containing `skinId`
- deadline: **10,000 ms**
- no handler retry loop.

After the provider result completes successfully, native reads/projects the current VIP18 config, patches only:

- `selectedSkinId = skinId`

and routes that normalized config through the same VIP18 save helper used by `vip18_base_config_save`.

## Restore

Restore sends one provider request to:

- method: `restoreLocalBaseSkin`
- arguments: empty object
- deadline: **10,000 ms**
- no handler retry loop.

After successful provider completion, native reads/projects the current VIP18 config and applies this exact retained patch:

- `selectedSkinId = null`
- `autoApplyOnStart = false`

The remaining retained config fields are preserved through the shared config projection/save path.

## Ordering and success result

The game provider action happens **before** the VIP18 config persistence step.

Therefore provider success followed by unavailable/failed shared config state is not atomic: the in-game base skin action can already have happened even though the public command ultimately fails during config reconciliation.

On full success, both commands return the original provider result through the shared generic JSON converter. They do **not** replace it with the three-field VIP18 config projection.

The shared `config.json` owner remains the same incomplete migration/normalization lane documented by R8-043/R8-049/R8-061, including exact `STATE_UNAVAILABLE / config state is unavailable` behavior where that owner cannot be accessed.

## Why no production route is added

An exact implementation would require both:

1. mandatory owner-excluded authorization-state admission before the live provider action; and
2. the original shared config-state migration/merge/write semantics after provider success.

Bypassing admission or substituting a standalone VIP18 JSON file would change observable behavior and side-effect ordering.

R8-072 therefore adds no runtime route. `vip18_base_apply` and `vip18_base_restore` move from genuinely unclosed to audited/fenced.

Remaining genuinely-unclosed retained frontend routing gaps: **5**.

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-072-vip18-base-actions-fence.json`.
