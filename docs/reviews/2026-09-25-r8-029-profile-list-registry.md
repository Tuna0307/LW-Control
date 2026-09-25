# R8-029 — restore profile list registry path

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** replace the rebuild's synthetic one-profile `profile_list` payload with a local controller-registry implementation for the normal retained single-profile path.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_list` handler at VA `0x14014bd06`;
- native profile-list query helper at VA `0x1403d943e`;
- native public-profile mapping path at VA `0x14041cbb1`;
- native profile-list normalization path at VA `0x14041a971`;
- original installed `controller.db` schema and `selected_profile_id` controller state;
- retained original frontend profile consumers.

No frontend asset changed in this checkpoint.
## Original controller registry

The original controller database contains:

```sql
CREATE TABLE controller_state (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);

CREATE TABLE profiles (
  id TEXT PRIMARY KEY,
  display_name TEXT NOT NULL,
  role_name TEXT,
  server_id TEXT,
  game_uid TEXT UNIQUE,
  note TEXT NOT NULL DEFAULT '',
  display_order INTEGER NOT NULL,
  enabled INTEGER NOT NULL DEFAULT 1,
  locked_reason TEXT,
  is_primary INTEGER NOT NULL DEFAULT 0,
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL,
  last_launched_at INTEGER
);
```

The installed original controller state contains:

```text
selected_profile_id = wHR5C9cTgD0aC_2XegklLA
```

The installed profile is enabled, primary, unlocked, display order 0, and initially unbound to role/server/game UID.
## Exact list query and public profile fields

The native executable contains this list query:

```sql
SELECT id, display_name, role_name, server_id, game_uid, note,
       display_order, enabled, locked_reason, is_primary,
       created_at, updated_at, last_launched_at
FROM profiles
ORDER BY display_order, created_at, id
```

The public profile serializer exposes:

- `id`
- `displayName`
- `roleName`
- `serverId`
- `gameUid`
- `note`
- `displayOrder`
- `enabled`
- `lockedReason`
- `isPrimary`
- `lastLaunchedAt`

`created_at` and `updated_at` are database ordering/maintenance fields and are not exposed in the public profile object.

The profile-list state exposes `selectedProfileId`, `maxProfiles`, and `profiles`. The original frontend consumes all three.
## Rebuild implementation

R8-029 adds:

- `ProfileRegistryStore`;
- `ProfileRegistryCommandService`;
- production `controller.db` composition;
- deterministic `profile_list` regression coverage.

On first normal production composition the rebuild seeds its existing stable local `ProfileId` into the controller registry without changing that identity. The seed is idempotent: once a registry row exists, later startup does not overwrite display name, role/server binding, note, or other profile metadata.

The rebuild now returns the stored public profile fields instead of the previous synthetic object that added rebuild-only `connectionState`.

Because Account/Login/Authentication and entitlement behavior are explicitly excluded, the retained normal path fixes `maxProfiles=1`. This is a retained-scope adaptation, not a claim that original multi-entitlement quota logic is fully restored.
## Storage and selection boundary

R8-029 uses the rebuild's existing application-data namespace:

```text
%LOCALAPPDATA%\LWBridgeRebuild\controller.db
```

The original uses its own Roaming `lwbridge\controller.db`; filesystem-root parity is not claimed.

The seeded normal path also creates `controller_state.selected_profile_id` when absent, pointing to the stable current local profile.

This checkpoint does not claim the original repair/fallback behavior for a malformed, missing, locked, or stale selected-profile registry. It restores the normal initialized path only.

It also does not yet restore:

- `profile_select`;
- `profile_enable_set`;
- `profile_primary_set`;
- create/delete/note/reorder behavior;
- multi-profile entitlement locking;
- instance launch ownership.
## Regression coverage

`ProfileRegistryChecks` proves:

- native-compatible `controller_state` and `profiles` storage exists;
- the stable rebuild profile is seeded as enabled, primary, unlocked, display order 0;
- selected-profile state points to that profile;
- seeding is idempotent and preserves later profile metadata;
- `profile_list` returns `selectedProfileId`, `maxProfiles`, `profiles`;
- the public profile object exposes the recovered field set;
- rebuild-only `connectionState` is absent;
- database-only `createdAt` / `updatedAt` are absent;
- normal backend routing reaches the registry service.

## Validation

Completed before packaging:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.
