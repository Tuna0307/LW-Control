# R8-028 — restore profile settings save persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the local `profile_settings_save` host-side contract for the currently available rebuild profile: payload validation, profile lookup boundary, revisioned `settings` persistence, read-back result, and recovered error codes.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_settings_save` handler at VA `0x14011c732`;
- native profile-settings save helper at VA `0x1403d5ace`;
- native profile-existence helper at VA `0x1403d5862`;
- native profile-settings read helper at VA `0x1403dbc87`;
- original `controller.db` schema containing the central `profiles` table;
- original per-profile `profile.db` schema and installed settings row.

No frontend asset changed in this checkpoint.
## Original storage model

The original controller database contains the central profile registry:

```sql
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
)
```

The selected profile owns a separate `profile.db` containing:

```sql
CREATE TABLE settings (
  id INTEGER PRIMARY KEY CHECK(id = 1),
  revision INTEGER NOT NULL,
  value_json TEXT NOT NULL
)
```

The installed original profile starts with:

```text
id=1
revision=0
value_json={}
```

The same per-profile database also contains `profile_state`; settings updates must not disturb those rows.
## Public payload contract

The public handler requires:

- `profileId`;
- `revision`;
- `value`.

Bad/missing public payload fields use recovered code:

`INVALID_REQUEST`

The native helper separately verifies that the target profile exists. A missing profile uses:

`PROFILE_NOT_FOUND`

The settings `value` must be a JSON object. A non-object value uses:

`INVALID_PROFILE_SETTINGS`

The public revision is a non-negative integer and participates in optimistic concurrency.
## Exact revisioned update

The native executable contains this SQL verbatim:

```sql
UPDATE settings SET value_json = ?, revision = revision + 1
                 WHERE id = 1 AND revision = ?
```

Therefore a save succeeds only when the supplied revision matches the current singleton settings row.

If no row is updated, native 0.3.1 returns:

`PROFILE_REVISION_CONFLICT`

After a successful update, the native path reads the settings row again with:

```sql
SELECT revision, value_json FROM settings WHERE id = 1
```

and returns the updated settings record with:

```json
{
  "revision": 1,
  "value": {}
}
```

where `value` is the saved JSON object.
## Rebuild implementation

R8-028 adds:

- `ProfileSettingsStore`;
- `ProfileSettingsCommandService`;
- production routing for `profile_settings_save`;
- deterministic regression coverage.

The rebuild uses its already-established per-profile rebuild database root and creates/uses the original singleton `settings` table in that `profile.db`.

This checkpoint does **not** claim byte-for-byte filesystem-path parity with the original Roaming `lwbridge` root. It restores the observable settings schema/revision contract inside the rebuild's existing profile storage namespace.

Because the rebuild still exposes only its current local profile through normal production composition, R8-028 supports saving that current profile. A different `profileId` is surfaced as `PROFILE_NOT_FOUND` by the service rather than being silently accepted.

Full original multi-profile registry/select/enable/primary behavior remains separate work.
## Regression coverage

`ProfileSettingsChecks` proves:

- initial singleton settings row is revision 0 with `{}`;
- successful revision-0 save returns revision 1 and the saved object;
- settings survive a read from the same `profile.db`;
- existing `profile_state` data is preserved;
- stale revision returns `PROFILE_REVISION_CONFLICT`;
- unknown profile returns `PROFILE_NOT_FOUND`;
- non-object value returns `INVALID_PROFILE_SETTINGS`;
- negative/missing revision returns `INVALID_REQUEST`;
- normal backend routing reaches the new service;
- a different profile through the backend reaches `PROFILE_NOT_FOUND`, not the rebuild's prior generic profile-scope error.

## Validation

Completed before packaging:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.
## Still intentionally missing

R8-028 does not claim parity for:

- full original multi-profile list/select/create/delete/enable/primary behavior;
- original controller/profile application-data root path;
- exact native database-error mapping outside recovered normal/conflict/data-invalid cases;
- profile-settings frontend consumers not present in the retained current UI flow;
- profile instance launch/stop ownership beyond prior checkpoints;
- Account/Login/Authentication behavior.

Those remain separate retained-scope recovery work or explicit excluded scope.
