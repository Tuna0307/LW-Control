# R8-031 — restore profile reorder transaction

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the host-local `profile_reorder` validation, transactional display-order persistence, and refreshed profile-list result.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_reorder` handler at VA `0x14014f296`;
- registry wrapper at VA `0x14041c7ae`;
- native reorder core at VA `0x1403dade0`;
- shared profile-ID validator at VA `0x1403dc23e`;
- ASCII ID character predicate at VA `0x14020da92`;
- R8-029 profile-list/query contract.

No frontend asset changed.
## Public request boundary

The command expects a `profileIds` JSON array.

A missing `profileIds` property or a property that is not an array follows the recovered public handler error:

`INVALID_REQUEST`

The Rust handler then deserializes the array as strings. The exact generic Rust/Tauri wording for a malformed non-string array element is not claimed by this checkpoint; the rebuild rejects that malformed typed list as `INVALID_REQUEST` conservatively.

Each submitted string is passed through the native profile-ID validator before order validation.
## Exact profile-ID validation

The shared native validator accepts profile IDs only when:

- UTF-8 byte length is between 1 and 64 inclusive;
- every byte is ASCII `0-9`, `A-Z`, `a-z`, underscore, or hyphen.

Anything else returns:

`INVALID_PROFILE_ID`

This means spaces, non-ASCII characters, the empty string, and IDs longer than 64 bytes are rejected before permutation validation.
## Exact permutation validation

After validating every submitted ID, native 0.3.1 reads the current profile list and requires the submitted list to be an exact permutation of those rows.

Recovered checks establish all of the following:

- submitted count equals current profile count;
- submitted IDs are unique;
- every current profile ID exists in the submitted set.

Any duplicate, omission, extra ID, or count mismatch returns:

`INVALID_PROFILE_ORDER`

No reorder transaction begins when this validation fails.
## Exact transaction

The native core starts one transaction, captures one timestamp, and processes the submitted IDs in order.

The executable contains:

```sql
UPDATE profiles SET display_order = ?, updated_at = ? WHERE id = ?
```

The first submitted profile receives `display_order=0`, the next receives 1, and so on. Every row in the operation receives the same captured `updated_at` value. The transaction is committed after all updates.

After commit, the public handler refreshes and returns the complete profile-list state:

```text
{ selectedProfileId, maxProfiles, profiles }
```

Selection is not changed by reorder.
## Rebuild implementation and checks

R8-031 extends the R8-029/R8-030 controller registry with:

- exact valid profile-ID admission;
- exact permutation validation;
- a single SQLite transaction using one shared timestamp;
- zero-based `display_order` assignment;
- `profile_reorder` backend routing;
- refreshed-list return behavior.

Deterministic tests use three local fixture profiles and prove:

- requested order is persisted and returned;
- display orders are 0, 1, 2;
- selected profile remains unchanged;
- all rows receive the same update timestamp;
- duplicates, omissions, extras and empty order fail with `INVALID_PROFILE_ORDER`;
- spaces, empty IDs and >64-byte IDs fail with `INVALID_PROFILE_ID`;
- malformed top-level `profileIds` fails with `INVALID_REQUEST`;
- failed validation does not alter prior order;
- backend routing reaches the reorder service.
## Validation

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.

## Still intentionally missing

R8-031 does not restore:

- `profile_select` focus behavior;
- `profile_primary_set` fixed-primary invariants;
- `profile_enable_set` entitlement/capacity behavior;
- profile create/delete behavior;
- launcher/game instance ownership.
