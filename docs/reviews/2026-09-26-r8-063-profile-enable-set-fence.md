# R8-063 - fence profile_enable_set at authorization capacity boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the native profile-capacity reconciliation command without fabricating license/entitlement capacity or exposing an account-derived capacity mutation path.

## Native authority

Primary evidence:

- public `profile_enable_set` handler `0x1401101EF-0x140110C7D`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- authorization capacity extractor `0x1400DE81A-0x1400DE8EC`;
- `profileIds` JSON sequence parser `0x14028BFB8`;
- profile-capacity service `0x140419180-0x14041928A`;
- controller/profile-capacity core `0x1403D614E-0x1403D6E4B`;
- profile-list loader `0x1403D943E-0x1403D9748`;
- selected-profile repair helper `0x1403DA8A1-0x1403DABDA`;
- profile-list projector/serializer `0x14041C193` / `0x14042E54C`.

## Public command shape

`profile_enable_set` is not a `{profileId, enabled}` toggle. Native requires payload field:

`profileIds: [...]`

The field must be a JSON array/sequence. A missing/wrong-container value is rejected through the handler's `INVALID_REQUEST` path. The exact serde error detail for individual non-string elements is not closed and is not guessed.

The normal recovered frontend profile API block does not expose this command as a standalone toggle; the visible UI instead consumes entitlement `maxProfiles` and per-profile `enabled` / `lockedReason` state. The native command is therefore best understood as a capacity-reconciliation operation.

## Authorization/capacity admission

Native awaits shared authorization state before applying profile-capacity changes. Unavailable state uses the already recovered exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

The authorization-derived capacity extractor supplies the capacity passed into the controller mutation core. The native core accepts only capacities **1, 2, or 5**; any other value returns exact code `INVALID_PROFILE_CAPACITY`.

That capacity is license/entitlement-derived. Its source/policy is owner-excluded and R8-063 does not invent a fixed capacity.

## Profile-list and primary invariant

The mutation core first loads the native profile registry using the same full profile-list query family as R8-029. It requires an existing primary profile; absence returns exact code `PROFILE_PRIMARY_MISSING`.

The requested enabled set is then derived as follows:

- when total profiles are less than or equal to capacity, native uses all profiles as the enabled set;
- when total profiles exceed capacity and capacity is `1`, native automatically keeps the primary profile enabled;
- when total profiles exceed capacity at `2` or `5`, the submitted `profileIds` set must resolve to exactly `capacity` unique profile IDs, must contain the primary profile, and must contain only IDs present in the registry;
- failure of those over-capacity selection checks returns exact code `PROFILE_SELECTION_REQUIRED`.

Duplicate submitted IDs therefore cannot satisfy an exact-capacity unique selection when they collapse the set below capacity.

## Transactional row mutation

Native begins the profile-capacity transaction and updates every profile row with exact SQL:

`UPDATE profiles SET enabled = ?, locked_reason = ?, updated_at = ? WHERE id = ?`

For each profile:

- membership in the resolved enabled set -> `enabled = true`, `locked_reason = NULL`;
- not in the enabled set -> `enabled = false`, `locked_reason = "license_capacity"`;
- `updated_at` is current Unix milliseconds from native helper `0x14023F1C0`.

The mutation is committed under the native `begin profile capacity update` / `update profile capacity` / `commit profile capacity` error contexts.

Despite nearby string-table text about blank-profile rollback, the recovered `0x1403D614E` function does **not** delete profile rows; that text belongs to adjacent helpers/contexts and is not claimed as part of public `profile_enable_set`.

## Selected-profile repair

After the capacity transaction commits, native reads the current `selected_profile_id`. If the selected profile remains enabled and has no lock reason, it is kept.

If the selected profile became locked/disabled by the capacity update, native uses the selected-profile repair path and writes the surviving primary profile as the new selection. The helper's direct lock check is exact:

`SELECT enabled = 1 AND locked_reason IS NULL FROM profiles WHERE id = ?`

and its failure vocabulary includes `PROFILE_LOCKED`.

## Success result

After mutation/selection repair, the public handler continues through the same native profile-list projector and JSON serializer used by `profile_list`.

Successful result is therefore the refreshed profile-list state with exact top-level fields:

`selectedProfileId`, `maxProfiles`, `profiles`.

`maxProfiles` remains authorization/entitlement-derived; R8-063 does not replace it with a rebuild constant.

## Why no production implementation is added

The local SQL and selection semantics are now sufficiently closed, but the command's governing capacity comes directly from owner-excluded authorization/license state. Any rebuild implementation would need either to fabricate capacity or to recreate excluded entitlement behavior.

R8-063 therefore makes no runtime-code change. The command stays fenced until the capacity input can be represented without reconstructing account/license functionality.

## Evidence classification

- `profileIds` array container requirement: `EXACT_NATIVE`;
- individual serde element error detail: `UNKNOWN`;
- capacity domain `{1,2,5}` + `INVALID_PROFILE_CAPACITY`: `EXACT_NATIVE`;
- primary requirement + `PROFILE_PRIMARY_MISSING`: `EXACT_NATIVE`;
- over-capacity selection rules + `PROFILE_SELECTION_REQUIRED`: `EXACT_NATIVE`;
- per-row `enabled` / `locked_reason="license_capacity"` SQL update: `EXACT_NATIVE`;
- selected-profile repair: `EXACT_NATIVE`;
- refreshed profile-list success projection: `EXACT_NATIVE`;
- authorization/license capacity source: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.