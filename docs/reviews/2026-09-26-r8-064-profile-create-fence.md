# R8-064 - fence profile_create at authorization capacity/runtime boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close native `profile_create` capacity admission, controller-row creation/defaults, generated identity, public created-profile result and post-create runtime boundary without fabricating entitlement capacity.

## Native authority

Primary evidence:

- `profile_create` handler `0x140156D0A-0x140157871`;
- authorization/capacity source `0x1400DE81A-0x1400DE8EC`;
- capacity/create service `0x14041C341-0x14041C51E`;
- controller create helper `0x1403D4A7F-0x1403D51D6`;
- generated-ID helper `0x1403DD939-0x1403DD9B1` and Base64 encoder `0x140401E98-0x140401FC4`;
- native profile-row serializer `0x1403DC2A1-0x1403DC575`;
- post-create runtime/state helper `0x1403AC07B-0x1403AC626`;
- retained frontend `profile_create` wrapper and profile-context `create()` flow.

## Authorization and capacity admission

`profile_create` has no command-specific input fields. Native first depends on the shared authorization/profile state and obtains the account's profile capacity from that state.

The capacity is a byte-sized `maxProfiles` value extracted from the authorization/entitlement subsystem. Native loads the current controller profile list and compares its count directly against that capacity:

- when `profileCount >= maxProfiles`, creation fails with exact `PROFILE_LIMIT_REACHED`;
- only when `profileCount < maxProfiles` does native call the controller create routine.

The same service path can surface `STATE_UNAVAILABLE` when the required shared state is unavailable.

The capacity source/policy belongs to owner-excluded account/license behavior. R8-064 does not replace it with the rebuild's retained single-profile `maxProfiles=1` adaptation.

## Generated profile identity

Native generates exactly 16 random bytes, then encodes them with the URL-safe Base64 alphabet:

`ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_`

The encoder configuration is no-padding. A 16-byte identifier therefore becomes a 22-character Base64URL string. This matches installed native profile IDs such as the recovered R8-029 sample.

## Display order and default display name

Native reads the next order with exact SQL:

`SELECT COALESCE(MAX(display_order) + 1, 0) FROM profiles`

The new row's `display_order` is that value. The default display-name formatter uses the exact UTF-8 literal `账号 ` followed by `display_order + 1`.

Examples:

- first additional profile at `display_order=1` -> `账号 2`;
- next at `display_order=2` -> `账号 3`.

The original localized literal is preserved; R8-064 does not normalize it to `Profile N` or `Account N`.

## Exact controller insert/defaults

Native inserts with exact SQL:

```sql
INSERT INTO profiles
       (id, display_name, display_order, enabled, locked_reason,
        is_primary, created_at, updated_at)
     VALUES (?, ?, ?, 1, NULL, 0, ?, ?)
```

Combined with the native table defaults recovered in R8-029, a newly created row therefore starts with:

- generated `id`;
- generated `display_name`;
- `role_name = NULL`;
- `server_id = NULL`;
- `game_uid = NULL`;
- `note = ''`;
- next `display_order`;
- `enabled = true`;
- `locked_reason = NULL`;
- `is_primary = false`;
- `created_at` / `updated_at` set by the create routine;
- `last_launched_at = NULL`.

The create helper creates the profile root and `profile.db` as part of the local create path. Its post-insert read helper re-reads the created profile row; it is not a selected-profile repair helper.

## Public success result

The successful create branch serializes the created native profile record with exact field order:

1. `id`
2. `displayName`
3. `roleName`
4. `serverId`
5. `gameUid`
6. `note`
7. `displayOrder`
8. `enabled`
9. `lockedReason`
10. `isPrimary`
11. `createdAt`
12. `updatedAt`
13. `lastLaunchedAt`

This create result is intentionally not the R8-029 `profile_list` row shape: `profile_list` omits `createdAt` and `updatedAt`, while the create serializer includes them.

The retained frontend confirms direct success-object ownership:

`let e = await profile_create(); profile_select(e.id)`

Thus the create result must expose the new profile `id`, and the UI explicitly selects it in a separate command after creation. The controller create SQL itself does not update `selected_profile_id`.

## Post-create runtime/state boundary

After local creation/serialization, the public handler invokes native post-create runtime/state provisioning through `0x1403AC07B`. That path can return `STATE_UNAVAILABLE` and performs substantial runtime reconciliation rather than a simple profile-list refresh.

The exact failure cleanup/rollback behavior across controller row, profile directory and runtime provisioning is not yet closed one-for-one. R8-064 therefore does not claim that every post-create failure either persists or rolls back the local row.

## Why no production implementation is added

The local create SQL/defaults, generated identifier, naming and public created-profile projection are now recoverable, but public admission still requires authorization/entitlement-derived `maxProfiles`, and the post-create runtime provisioning path is not fully recovered.

Using the rebuild's fixed retained quota as the native capacity would make `PROFILE_LIMIT_REACHED` behavior wrong. Skipping the native post-create state/runtime phase would also change observable failure behavior.

R8-064 therefore makes no runtime-code change.

## Evidence classification

- authorization/profile-state dependency: `EXACT_NATIVE`;
- capacity comparison + `PROFILE_LIMIT_REACHED`: `EXACT_NATIVE`;
- entitlement capacity source/policy: `OWNER_EXCLUDED_UNKNOWN`;
- 16-byte Base64URL-no-pad profile ID: `EXACT_NATIVE`;
- next display-order query: `EXACT_NATIVE`;
- `账号 {displayOrder+1}` default display name: `EXACT_NATIVE`;
- profile insert SQL/defaults: `EXACT_NATIVE`;
- 13-field created-profile success projection: `EXACT_NATIVE`;
- frontend separate `profile_select(created.id)`: `EXACT_BYTES`;
- post-create runtime/state phase existence + `STATE_UNAVAILABLE`: `EXACT_NATIVE`;
- exact post-create failure cleanup/rollback semantics: `UNKNOWN`;
- runtime implementation: `FENCED`.