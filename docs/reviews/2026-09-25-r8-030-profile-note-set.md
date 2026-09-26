# R8-030 — restore profile note persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the host-local `profile_note_set` contract on the controller profile registry.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_note_set` handler at VA `0x14015cd66`;
- native note-save helper at VA `0x1403db619`;
- native refreshed profile-list result helper at VA `0x14041c193`;
- R8-029 recovered controller registry/list contract.

No frontend asset changed in this checkpoint.
## Public request and result

The public handler requires string fields:

- `profileId`
- `note`

Missing or wrong-type required fields use the public command error:

`INVALID_REQUEST`

On success, the command does not return only the changed row. The handler refreshes and returns the full profile-list state:

```text
{ selectedProfileId, maxProfiles, profiles }
```

This matches the original frontend, which replaces its profile state with the command result after updating a note.
## Exact note validation

The native helper counts Unicode scalar values (Rust `char` semantics), not UTF-8 bytes and not UTF-16 code units.

The accepted note length is 0 through 80 Unicode scalars inclusive.

A note is rejected with:

`INVALID_PROFILE_NOTE`

when either:

- it contains more than 80 Unicode scalars;
- it contains a code point below U+0020;
- it contains a C1 control from U+007F through U+009F inclusive.

Therefore:

- an empty note is valid;
- 80 emoji such as 😀 are valid even though each is two UTF-16 code units in .NET;
- 81 such emoji are invalid;
- U+00A0 is allowed.
## Exact persistence behavior

The native executable contains this SQL:

```sql
UPDATE profiles SET note = ?, updated_at = ? WHERE id = ?
```

The rebuild preserves that observable update contract in its R8-029 controller registry.

If no profile row is updated, native 0.3.1 returns:

`PROFILE_NOT_FOUND`

After a successful update, the public handler rebuilds the normal profile-list result, preserving `selectedProfileId` and the list quota/state.

The note value is stored as supplied after validation; this checkpoint does not add trimming or normalization not present in the recovered helper.
## Rebuild implementation

R8-030 extends:

- `ProfileRegistryStore` with the recovered note/update timestamp mutation;
- `ProfileRegistryCommandService` with `profile_note_set`;
- the backend global command allowlist;
- deterministic profile-registry checks.

The implementation uses `.EnumerateRunes()` so the validation boundary follows Unicode scalar values rather than UTF-16 `string.Length`.

As in R8-029, the controller DB remains in the rebuild's existing `%LOCALAPPDATA%\LWBridgeRebuild\controller.db` namespace. Original filesystem-root parity is not claimed.
## Regression coverage

The deterministic checks prove:

- normal note persistence and refreshed list response;
- selected profile remains unchanged;
- `updated_at` changes on save;
- 80 non-BMP Unicode scalars accepted;
- 81 rejected with `INVALID_PROFILE_NOTE`;
- U+001F, U+007F and U+009F rejected;
- U+00A0 accepted;
- empty note accepted;
- missing profile returns `PROFILE_NOT_FOUND`;
- missing/wrong-type required fields return `INVALID_REQUEST`;
- backend routing reaches `profile_note_set`.

## Validation

Completed before packaging:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.
## Still intentionally missing

R8-030 does not claim parity for:

- `profile_reorder` (native validation/transaction path is recovered in part and is the next local candidate);
- `profile_select` (has an additional `focusGame` side effect);
- `profile_primary_set` (contains `PROFILE_PRIMARY_FIXED` invariants);
- `profile_enable_set` and entitlement/capacity behavior;
- profile create/delete behavior;
- launcher/game instance ownership.
