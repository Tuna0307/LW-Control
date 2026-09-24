# R8-021 — restore updater idle-status contract

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** correct the production `update_status` idle envelope to the recovered original 0.3.1 contract. Network update checks, download, executable launch and updater event transitions remain intentionally unimplemented.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable original `index-sfL2sT3K.js` update-panel state/field consumption;
- immutable API wrappers for `update_status`, `update_check`, `update_download_and_open` and `bridge://update-status`;
- native 0.3.1 update-status serializer around VA `0x140243af5`;
- native updater construction/check transition code in `src\services\update.rs`, including the idle constructor around VA `0x1402426b4`;
- reference executable FileVersion/ProductVersion, both exactly `0.3.1`.

R8-021 changes no frontend asset. The whole current `index` bundle is not classified as byte-identical because earlier intentional frontend transforms exist:

- immutable original SHA-256: `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`;
- current rebuild SHA-256 at this checkpoint: `54ef8a40a45cfe6a91196c6a6be31c1f783a01388d65ecf1f48c9da00ed28614`.

The update-panel contract in this review is therefore **source-derived from the immutable original**, not claimed as whole-bundle `EXACT_BYTES`.

## Recovered public status object

The immutable frontend initializes and consumes these nine fields:

```json
{
  "phase": "idle",
  "currentVersion": "",
  "latestVersion": null,
  "releaseNotes": "",
  "publishedAt": null,
  "progress": null,
  "message": null,
  "nextManualCheckAt": null,
  "downloadDirectory": ""
}
```

That frontend object is only a UI fallback. The native serializer proves the production command returns exactly the same nine property names:

- `phase`;
- `currentVersion`;
- `latestVersion`;
- `releaseNotes`;
- `publishedAt`;
- `progress`;
- `message`;
- `nextManualCheckAt`;
- `downloadDirectory`.

The R8-020-and-earlier rebuild status was not exact: it omitted `publishedAt` and returned the rebuild-only string `0.3.1-rebuild`.

## Native idle initialization

The native updater constructor materializes `idle` directly and initializes the optional status members to their empty/null state before the update service is exposed.

Recovered idle values for the verified reference are:

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

`currentVersion` is not a hard-coded UI placeholder in the native implementation: the updater constructor receives the application/package version, parses it, and clones the supplied version into the status object. The verified reference executable reports both FileVersion and ProductVersion as `0.3.1`, so `0.3.1` is exact for this reconstruction target.

## Native updater behavior recovered but not implemented here

The native update service also exposes evidence for real updater state transitions and errors, including:

- phases `checking`, `opening`, `upToDate`, `available`, `error`;
- `bridge://update-status` events;
- a 60,000 ms manual-check cooldown calculation;
- `updates` as the update directory family;
- portable output naming `lwbridge-<version>.exe`;
- `UPDATE_SIGNATURE_INVALID`;
- `UPDATE_MANIFEST_INVALID`;
- `UPDATE_NOT_AVAILABLE`;
- `UPDATE_URL_INVALID`;
- `UPDATE_HOST_INVALID`;
- `UPDATE_VERSION_INVALID`;
- `UPDATE_OPEN_FAILED`;
- default update host `download.songunity.com`.

Those strings/transitions are evidence only in R8-021. They are **not** sufficient by themselves to claim the complete network protocol, signature verification, file replacement, process handoff, cooldown persistence or executable-opening semantics.

## Production behavior after R8-021

`update_status` now returns the exact recovered idle object for the verified 0.3.1 reference.

`update_check` and `update_download_and_open` remain fenced and fall through to the production backend's existing:

- code: `COMMAND_NOT_IMPLEMENTED`.

This is intentionally not claimed as native error parity. It prevents the rebuild from inventing a successful update/download/open workflow before the complete updater contract is recovered.

## Regression coverage

`UpdateStatusChecks` proves:

- exactly nine public status properties;
- `phase = idle`;
- `currentVersion = 0.3.1`;
- null `latestVersion`;
- empty `releaseNotes`;
- null `publishedAt`;
- null `progress`;
- null `message`;
- null `nextManualCheckAt`;
- empty `downloadDirectory`;
- `update_check` remains `COMMAND_NOT_IMPLEMENTED`;
- `update_download_and_open` remains `COMMAND_NOT_IMPLEMENTED`.

The complete deterministic suite remains green.

## Still outside this checkpoint

R8-021 does not claim parity for:

- manifest endpoint/request semantics;
- signature/public-key verification;
- version comparison beyond the recovered idle reference version;
- check cooldown persistence;
- update download streaming/progress;
- file hash/size verification;
- update file placement/replacement;
- executable/process handoff;
- desktop shortcut replacement;
- `bridge://update-status` transition timing;
- updater error mapping beyond the recovered evidence inventory.

## Validation

Completed before packaging:

- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed in this checkpoint.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
