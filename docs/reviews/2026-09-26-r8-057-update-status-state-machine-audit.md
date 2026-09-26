# R8-057 - strict audit update_status state machine

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the native stateful `update_status` snapshot/transition contract beyond the R8-021 idle-only implementation, without enabling `update_check` or `update_download_and_open`.

## Native authority

Primary evidence:

- public `update_status` handler `0x14018B725-0x14018BDA8`;
- updater-state copier `0x140242EAC-0x14024306C`;
- exact nine-field JSON serializer `0x140243AF5-0x140243D04`;
- updater constructor / idle initialization around `0x1402426B4`;
- status transition helpers `0x140240BAA-0x140241460`;
- manual-check cooldown helper `0x140241460-0x14024169B`;
- update-check manifest/result path including `0x1402419C9` and `0x140240B59 -> 0x14024126F`;
- `update_check` handler `0x140159201-0x140159C82`;
- `update_download_and_open` handler `0x14011DC39-0x14011FCDF`;
- retained frontend `update_status`, `update_check`, `update_download_and_open` and `bridge://update-status` wrappers.

## Public update_status behavior

`update_status` is read-only. It snapshots the updater service state and returns it; it does not itself start a network check, download, verification or executable launch.

The public JSON serializer emits exactly nine fields in this order:

1. `phase`
2. `currentVersion`
3. `latestVersion`
4. `releaseNotes`
5. `publishedAt`
6. `progress`
7. `message`
8. `nextManualCheckAt`
9. `downloadDirectory`

The R8-021 idle object remains exact for the verified 0.3.1 reference:

```json
{
  "phase": "idle",
  "currentVersion": "0.3.1",
  "latestVersion": null,
  "releaseNotes": "",
  "publishedAt": null,
  "progress": null,
  "message": null,
  "nextManualCheckAt": null,
  "downloadDirectory": ""
}
```

## Exact native phase machine

R8-057 upgrades the earlier phase-string inventory into transition-code evidence. The native updater has these observable phases:

- `idle`
- `checking`
- `upToDate`
- `available`
- `downloading`
- `opening`
- `error`

`opening` is confirmed by inline bytes in transition helper `0x140240BAA`; it is not dependent on a standalone referenced string.

### checking

Helper `0x140240CB0`:

- sets `phase = "checking"`;
- clears `progress`;
- clears `message`;
- computes `nextManualCheckAt` from the sampled clock plus exactly **60,000 ms**;
- snapshots the state;
- emits `bridge://update-status`.

The manual-check admission helper `0x140241460` compares the current clock with `nextManualCheckAt`. If the cooldown has not expired, it returns the current status snapshot without starting another check. Otherwise it enters `checking` and establishes a new 60-second deadline.

### upToDate / available

The parsed manifest success wrapper calls `0x14024126F` with an update-available flag:

- flag false / zero -> `phase = "upToDate"`;
- flag true / nonzero -> `phase = "available"`.

Both branches populate the manifest-derived:

- `latestVersion`;
- `releaseNotes`;
- `publishedAt`;

and clear:

- `progress`;
- `message`.

The transition then snapshots and emits `bridge://update-status`.

### error

Two exact error setters are recovered:

- `0x140240E01`: sets `phase = "error"` with fixed message `UPDATE_NOT_AVAILABLE`;
- `0x140240F3D`: sets `phase = "error"` with a supplied native/service error detail string.

Both clear progress and publish the updated status event.

The updater's recovered error vocabulary includes:

- `UPDATE_SIGNATURE_INVALID`
- `UPDATE_MANIFEST_INVALID`
- `UPDATE_NOT_AVAILABLE`
- `UPDATE_URL_INVALID`
- `UPDATE_HOST_INVALID`
- `UPDATE_VERSION_INVALID`
- `UPDATE_OPEN_FAILED`
- `UPDATE_DOWNLOAD_FAILED`
- `UPDATE_SIZE_MISMATCH`
- `UPDATE_HASH_MISMATCH`
- `UPDATE_TARGET_IS_CURRENT`
- `UPDATE_DOWNLOAD_TIMEOUT`

This checkpoint records those as native action/status evidence; it does not claim every triggering precedence branch is fully recovered.

### downloading

Helper `0x140241162`:

- sets `phase = "downloading"`;
- initializes `progress = 0`;
- clears `message`;
- snapshots and emits the event.

Helper `0x140241046` updates numeric progress and republishes the snapshot.

Helper `0x1402410AE` updates `downloadDirectory` and republishes. The live download handler calls this helper at `0x14011ED2D`.

### opening

After successful download/verification and before the executable-open helper, the download handler calls `0x140240BAA` at `0x14011F85C`.

That transition:

- sets `phase = "opening"`;
- sets `progress = 100`;
- clears `message`;
- snapshots and emits `bridge://update-status`.

The handler then calls the executable-open helper. `UPDATE_OPEN_FAILED` is recovered from that path; failures subsequently enter the generic `error` transition.

## Related action boundaries

`update_check` uses exact service path:

`/api/updates/current`

The retained updater service also owns manifest/signature verification, version comparison, host validation, download streaming, size/hash verification, output placement and process handoff.

Those action paths are not implemented by R8-057. This checkpoint only recovers enough of their static transitions to define what `update_status` can publish.

## Current rebuild comparison

The current production rebuild always returns the R8-021 exact idle object.

That is exact only while the updater is genuinely idle. It cannot represent native:

- `checking`;
- cooldown-backed `nextManualCheckAt`;
- `upToDate`;
- `available` manifest metadata;
- `downloading` and progress updates;
- `downloadDirectory`;
- `opening`;
- `error` and native error messages;
- `bridge://update-status` transition publication.

Adding a mutable status service without the native check/download/open owners would create synthetic transitions, so R8-057 makes no runtime change.

`update_check` and `update_download_and_open` remain fenced as before.

## Evidence classification

- nine-field serializer and order: `EXACT_NATIVE`;
- idle defaults: `EXACT_NATIVE`;
- read-only update_status ownership: `EXACT_NATIVE`;
- seven native phases: `EXACT_NATIVE`;
- 60,000 ms manual-check cooldown transition: `EXACT_NATIVE`;
- available/upToDate manifest projection: `EXACT_NATIVE`;
- downloading/progress/downloadDirectory/opening transitions: `EXACT_NATIVE`;
- update-status event publication: `EXACT_NATIVE`;
- full update-check protocol and all error precedence: `PARTIAL_EXACT_NATIVE / FENCED`;
- download/verify/open execution: `PARTIAL_EXACT_NATIVE / FENCED`;
- current rebuild non-idle update_status parity: `DEVIATION`.
