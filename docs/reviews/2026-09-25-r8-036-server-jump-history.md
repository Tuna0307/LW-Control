# R8-036 — restore server-jump history storage and migration semantics

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** replace the rebuild-only LocalConfig history owner with the original per-profile map database app-setting contract and restore get/set/import behavior.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `server_jump_history_get` handler at VA `0x14013E893`;
- `server_jump_history_import` handler at VA `0x140153890`;
- `server_jump_history_set` handler at VA `0x140154EF0`;
- profile-runtime resolver at VA `0x1402AE43C`;
- history normalizer at VA `0x1402313E7`;
- app-setting read helper at VA `0x14025593C`;
- app-setting write helper at VA `0x140255E7B`;
- retained original React frontend and its legacy migration key `lastwar.serverJumpHistory`.
## Storage owner

Native 0.3.1 resolves the requested `profileId` through the profile-runtime map, then uses that runtime's map-data database handle.

The history value is the `serverJumpHistory` entry in the existing `app_settings` table inside per-profile `map-data.db`.

Recovered read SQL:

```sql
SELECT value_json FROM app_settings WHERE key=?1
```

Recovered write SQL:

```sql
INSERT INTO app_settings(key,value_json,updated_at)
VALUES (?1,?2,?3)
ON CONFLICT(key) DO UPDATE SET
  value_json=excluded.value_json,
  updated_at=excluded.updated_at
```

The rebuild already had the recovered per-profile `map-data.db` schema, including `app_settings(key,value_json,updated_at)`. R8-036 reuses that owner rather than creating another file.
## Normalization

The native history normalizer keeps at most five server IDs.

For every supplied array element it:

- accepts only numeric integer IDs;
- requires `1 <= serverId <= 99999`;
- ignores invalid or out-of-range values;
- removes duplicates while preserving first-seen order;
- stops after five accepted IDs.

A missing or non-array `history` value normalizes to an empty list. It is not an `INVALID_PAYLOAD` error.

Malformed JSON already stored in `app_settings.value_json` reaches the native `INVALID_SETTING` error family. R8-036 preserves the stable code without claiming byte-for-byte framework-generated detail text.
## Command behavior

### server_jump_history_get

- resolve the profile runtime;
- read `serverJumpHistory`;
- missing key becomes `[]`;
- normalize the value;
- return the resulting array;
- a missing key is not written merely by get.

### server_jump_history_set

- resolve the profile runtime;
- normalize payload `history`;
- upsert the normalized array into `app_settings`;
- return that array.

### server_jump_history_import

This is a migration operation, not an alias for set.

- resolve the profile runtime;
- if `serverJumpHistory` already exists, return the existing stored history without overwriting it;
- otherwise normalize the supplied legacy history, persist it, and return it.
## Frontend migration proof

The retained frontend stores the old browser-side history under:

`lastwar.serverJumpHistory`

When a profile becomes active it:

1. reads and normalizes that legacy localStorage array;
2. invokes `server_jump_history_import(history, profileId)`;
3. adopts the returned native history;
4. removes the legacy localStorage key only after import succeeds.

Normal successful server jumps instead call `server_jump_history_set` with the new server first, prior duplicates removed, and a five-item cap.

This frontend flow independently confirms the native import-if-missing contract.
## Profile/runtime errors

The bridge-pipe resolver provides stable native codes:

- absent, blank or non-string `profileId` -> `PROFILE_ID_REQUIRED`;
- no matching owned profile runtime -> `PROFILE_RUNTIME_UNAVAILABLE`.

The adjacent binary evidence does not establish fixed human-readable message text for those two codes, so R8-036 claims code parity only.

For the retained single-profile rebuild, the current profile's existing `MapDataStore` is the equivalent runtime-owned database handle. An unknown profile or absent store returns `PROFILE_RUNTIME_UNAVAILABLE`.
## Implementation and validation

R8-036 adds app-setting read/upsert primitives to `MapDataStore`, adds `ServerJumpHistoryCommandService`, adds public `server_jump_history_get`, and redirects set/import away from `LocalConfigStore.ServerJumpHistory`.

The obsolete LocalConfig field remains readable only for backward compatibility; public history commands no longer read or write it.

Deterministic coverage proves missing-key get, normalization, missing/non-array -> empty, durable app-setting persistence, import-if-missing, existing-setting-wins, native profile error codes, corrupt-setting `INVALID_SETTING`, and backend routing.

Validation:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.
