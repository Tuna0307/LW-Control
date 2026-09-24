# R8-018 — restore City Layout draft persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1 City Layout contract
**Scope:** restore the exact City Layout draft key/table/revision behavior and the three draft commands only. Protected snapshot/planner/apply commands remain unimplemented.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`;
- immutable `CityLayoutPanel-B4B03XEi.js`;
- immutable City Layout API wrappers in `api-ClPPi2JT.js`.

The original frontend already remains byte-identical. R8-018 adds no City Layout frontend transform.
## Restored persistence

R8-018 adds a per-profile SQLite `profile.db` under the rebuild's existing profile directory:

`%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\profile.db`

This is the rebuild compatibility mapping for the recovered original **per-profile profile database** concept. The original absolute filesystem path is not claimed recovered.

Exact recovered table:

```sql
CREATE TABLE profile_state (
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  revision INTEGER NOT NULL
)
```

Exact City Layout key:

`city_layout_draft_v1`
## Restored commands

Production now handles:

- `city_layout_draft_get({profileId})`;
- `city_layout_draft_save({profileId,revision,value})`;
- `city_layout_draft_clear({profileId,revision})`.

The selected production profile is authoritative. A mismatched/missing profile identity fails with the recovered generic profile-state error code `INVALID_PROFILE_STATE`.

The command service is part of the normal production command composite and is disposed with the owning window/profile lifecycle. Isolated capture/probe modes do not create persistent draft state.
## Revision semantics

Save preserves the recovered optimistic-revision contract:

- missing row + expected revision 0 creates revision 1;
- existing row updates only where `revision = expectedRevision`;
- a successful update increments revision by exactly 1;
- stale creation/update returns `PROFILE_REVISION_CONFLICT`;
- a failed stale write does not alter the persisted value.

Clear executes the recovered guarded delete:

```sql
DELETE FROM profile_state
WHERE key = ? AND revision = ?
```

A stale/missing delete returns `PROFILE_REVISION_CONFLICT`.

Stored JSON is parsed on read. Malformed `value_json` returns `PROFILE_DATA_INVALID`.
## Missing-row representation

The helper report marks the original native no-row result shape as PARTIAL, while the frontend requires an object exposing `revision` and `value`.

R8-018 uses the minimal frontend-compatible empty record:

```json
{
  "profileId": "<selected>",
  "key": "city_layout_draft_v1",
  "revision": 0,
  "value": null
}
```

This exact no-row envelope is **EQUIVALENT/PARTIAL**, not claimed as recovered native bytes. The revision/value behavior is chosen because it matches the existing component's load/discard flow without inventing draft content.

Human-readable messages for generic profile-state error codes are likewise not claimed exact where the reference report only recovered the code.
## Regression coverage

`CityLayoutDraftChecks` proves:

- empty get returns selected profile/key plus revision 0/value null;
- first save creates revision 1;
- matching save increments to revision 2;
- stale save returns `PROFILE_REVISION_CONFLICT`;
- stale clear returns `PROFILE_REVISION_CONFLICT`;
- stale save cannot overwrite the latest value;
- profile mismatch returns `INVALID_PROFILE_STATE`;
- state persists after closing/reopening the same `profile.db`;
- matching clear removes the draft;
- malformed stored JSON returns `PROFILE_DATA_INVALID`.

Release build and the complete deterministic suite pass with this service installed in normal production routing.
## Still intentionally missing

R8-018 does **not** implement or fake:

- `city_layout_snapshot_get`;
- `city_layout_validate`;
- `city_layout_apply_start`;
- `city_layout_apply_status`;
- `city_layout_apply_cancel`;
- `getCityLayoutSnapshot`;
- `validateCityLayout`;
- `startCityLayoutApply`;
- `getCityLayoutApplyStatus`;
- `cancelCityLayoutApply`;
- any planner, temporary-move algorithm, zone logic, game move executor, rollback behavior, or cancellation state machine.

Because Refresh performs snapshot + draft + apply-status in parallel, the production City Layout feature is still not end-to-end functional. The restored draft layer is intentionally an independent first phase, matching the recovered implementation sequence.

## Validation

Completed before packaging:

- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
