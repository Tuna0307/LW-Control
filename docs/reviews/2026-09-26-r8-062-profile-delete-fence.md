# R8-062 - fence profile_delete at authorization-state admission

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the destructive local profile-delete contract, guard order, registry/selection mutation, filesystem cleanup, runtime reconciliation, and refreshed-list success result without bypassing native owner-excluded authorization-state admission.

## Native authority

Primary evidence:

- public `profile_delete` handler `0x14013F2BD-0x14013FCFB`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- required `profileId` helper `0x1403AC6C7-0x1403AC731`;
- runtime-running/delete service `0x14041C51E-0x14041C7AE`;
- controller delete transaction `0x1403DA488-0x1403DA8A1`;
- delete-target/selection helper `0x1403DCC3D-0x1403DD3B0`;
- post-delete runtime reconciliation helper `0x1404199BB-0x140419AFC`;
- profile-list projector `0x14041C193-0x14041C341`;
- profile-list JSON serializer `0x14042E54C-0x14042E668`;
- R8-029 native `profile_list` field/query evidence.

## Public admission order

The frontend sends `{profileId}`. Native first awaits shared authorization state, before parsing the payload field.

Unavailable authorization state returns exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

Only after authorization admission does native require JSON-string `profileId` through `0x1403AC6C7`. Missing or wrong-type `profileId` returns exact code `INVALID_REQUEST`; no extra human-readable detail is claimed.

Authorization/account-state implementation is owner-excluded, so R8-062 does not make the destructive command public in the rebuild.

## Runtime and target guards

Before opening the controller delete transaction, native checks managed runtime state. If the target profile is running, deletion fails with exact code/message `PROFILE_RUNNING`.

The delete-target query is exact:

`SELECT is_primary, game_uid IS NULL FROM profiles WHERE id = ?`

The public command then applies these target guards in order:

1. missing row -> `PROFILE_NOT_FOUND`;
2. primary profile -> `PROFILE_PRIMARY_REQUIRED`;
3. proceed to delete.

### Correction to the earlier R8-033 note

The generic delete-target helper contains an optional `PROFILE_ALREADY_BOUND` guard. However, the public `profile_delete` call passes that helper's `enforce unbound` flag as **false** (`[rsp+0x20] = 0` before `0x1403DCC3D`). Therefore native public `profile_delete` does **not** reject a bound non-primary profile merely because `game_uid` is present.

This supersedes the older broad R8-033 note that described bound-profile rejection as a public delete guard.

## Controller transaction and selected-profile repair

Native opens a controller transaction (`begin profile delete`), executes exact:

`DELETE FROM profiles WHERE id = ?`

and repairs selected-profile state inside the same local delete path. Native reads:

`SELECT value FROM controller_state WHERE key = 'selected_profile_id'`

If the deleted profile was selected, the restoration path points selection back to the surviving primary profile using the native controller-state update associated with `restore selected profile`. Because deleting the primary profile is already rejected, this preserves a valid primary fallback.

The transaction is then committed (`commit profile delete`).

## Filesystem cleanup ordering

After the controller transaction commits, native constructs the target profile-data path from the manager's profiles root plus `profileId` and attempts to remove the profile data.

Filesystem failure is wrapped as exact code `IO_ERROR` with `delete profile data` operation context.

This ordering matters: controller/profile-selection mutation is committed **before** filesystem cleanup. Therefore an `IO_ERROR` from profile-data removal can be returned after the registry row/selection change has already committed. R8-062 records that partial-failure behavior rather than pretending deletion is globally atomic.

## Post-delete runtime reconciliation

After local DB/filesystem deletion succeeds, native calls the runtime-map reconciliation helper for the deleted profile. That helper itself can surface `STATE_UNAVAILABLE` if its shared state is unavailable.

The handler releases/removes the returned runtime object when present, then continues through the same profile-list refresh/projection pipeline used by `profile_list`.

## Exact success result

Successful public deletion does not return null. It reaches the same native profile-list projector (`0x14041C193`) and JSON serializer (`0x14042E54C`) used by `profile_list`.

Therefore success is the refreshed profile-list state:

```json
{
  "selectedProfileId": "<current selected profile>",
  "maxProfiles": 1,
  "profiles": []
}
```

The example `maxProfiles` value above reflects the retained single-profile rebuild scope from R8-029; native quota ownership remains authorization/entitlement-derived. The exact native public keys are `selectedProfileId`, `maxProfiles`, and `profiles`, with profile rows using the R8-029 recovered field set.

## Why no production implementation is added

The rebuild has enough local registry primitives to approximate this mutation, but native authorization-state admission has precedence over payload validation and every destructive side effect. Bypassing that admission would make deletion callable in states where original LWBridge rejects it.

R8-062 therefore makes no runtime-code change. The local destructive contract is closed for future implementation only if the excluded admission can be represented without reconstructing account/auth behavior.

## Evidence classification

- authorization admission/error: `EXACT_NATIVE`;
- `profileId` required-string / `INVALID_REQUEST`: `EXACT_NATIVE`;
- `PROFILE_RUNNING`: `EXACT_NATIVE`;
- target SQL + `PROFILE_NOT_FOUND` / `PROFILE_PRIMARY_REQUIRED`: `EXACT_NATIVE`;
- public absence of bound-profile guard: `EXACT_NATIVE`;
- controller delete/selection-repair ordering: `EXACT_NATIVE`;
- post-commit profile-data cleanup + `IO_ERROR`: `EXACT_NATIVE`;
- post-delete runtime reconciliation: `EXACT_NATIVE` at observable boundary;
- refreshed profile-list success projection: `EXACT_NATIVE`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.
