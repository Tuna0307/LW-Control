# R8-022 — recover feedback-export public contract and fail-closed boundary

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** recover the original feedback-export command/result/progress-event contract and exact export-ID validation while keeping archive creation fenced until the native redaction/cache/archive/verification pipeline is fully recovered.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `SettingsPanel-CjjIQp-r.js`;
- immutable API wrappers in `api-ClPPi2JT.js`;
- native feedback result serializer near VA `0x14024b1ac`;
- native feedback progress serializer near VA `0x14024b2f6`;
- native `feedback_export` command registration/validation path near VA `0x1401a174e`;
- native `src\services\feedback.rs` string/data-path inventory.

The Settings panel remains byte-identical to immutable 0.3.1. SHA-256:

`669c614914cd7b5a00c5d71d1cd9996cdb300b6a2609768fb931f554929bd69e`

R8-022 changes no frontend asset.

## Exact frontend command/event contract

The original API exposes:

- `feedback_export({ exportId })`;
- listener `bridge://feedback-export-progress`.

The original Settings panel:

1. generates an `exportId` with `crypto.randomUUID()`;
2. immediately displays local `preparing / 0%` progress;
3. accepts progress events only when their `exportId` matches the current request;
4. treats a result with `canceled=true` as a return to idle;
5. displays success using `archiveBytes` and `path`;
6. treats command rejection as a generic feedback-export failure.

The UI-recognized progress states are:

- `preparing`;
- `exporting`;
- `finalizing`;
- `completed`.

## Exact native result serializer

The native result serializer emits exactly five public fields:

```text
canceled
path
fileCount
sourceBytes
archiveBytes
```

The Settings panel directly consumes `canceled`, `path`, and `archiveBytes`; `fileCount` and `sourceBytes` are still part of the native public result and therefore part of the parity contract.

R8-022 does not invent canceled-result default values that have not yet been pinned from the original command path.

## Exact native progress serializer

The native `bridge://feedback-export-progress` payload emits exactly five public fields:

```text
exportId
state
processedBytes
totalBytes
percent
```

This matches the immutable Settings panel's progress UI.

## Exact export-ID validation

The native command looks up the exact JSON field `exportId`, requires it to be a string, trims Unicode whitespace through the native UTF-8 whitespace helper, and rejects a missing, wrong-type, empty, or whitespace-only value with:

- code: `FEEDBACK_EXPORT_FAILED`;
- message: `export ID is required`.

R8-022 restores that exact invalid-input behavior.

The native implementation also has a single-export concurrency guard with:

- code: `FEEDBACK_EXPORT_IN_PROGRESS`;
- message: `a feedback archive is already being exported`.

The rebuild does not claim this runtime branch yet because no real archive executor is active, so there is no honest in-progress state to reproduce.

## Why archive creation remains fenced

Native evidence proves feedback export is not a simple “zip the logs” operation. The original service includes all of the following categories:

- a feedback redaction key and redaction processing;
- sensitive-token filtering/redaction vocabulary;
- cached feedback logs and cache metadata;
- per-profile runtime/config summaries;
- `diagnostics.json`;
- external game/launcher/updater/xLua log collection;
- rotated and segmented log handling;
- size/tail/retention limits;
- source/cache hashes and archive metadata;
- pending-export recovery;
- incomplete-archive cleanup/recovery;
- temporary/partial/segment files;
- save-location validation;
- archive-entry path validation;
- required-`diagnostics.json` verification;
- final archive verification/opening.

The recovered native path vocabulary includes `feedback-export.pending.json`, `feedback-redaction.key`, `log-cache/v1`, `logs/segments`, `.part`, `.zipseg`, and completed ZIP handling.

Implementing only a `SaveFileDialog` plus a generic ZIP would therefore create a misleading product behavior and could omit the native privacy/redaction guarantees. R8-022 explicitly does not do that.

## Production behavior after R8-022

For profile-scoped `feedback_export`:

- invalid/missing/blank `exportId` now returns the exact native `FEEDBACK_EXPORT_FAILED / export ID is required`;
- valid `exportId` remains fail-closed with rebuild `COMMAND_NOT_IMPLEMENTED`.

The latter is intentionally not claimed as native error parity. It is a safety/parity fence until the original archive pipeline is sufficiently recovered to implement without fabricating data collection or redaction semantics.

## Regression coverage

`FeedbackExportContractChecks` proves:

- missing `exportId` -> exact native failure;
- wrong-type `exportId` -> exact native failure;
- empty-string `exportId` -> exact native failure;
- ASCII/Unicode-whitespace-only `exportId` -> exact native failure;
- valid `exportId` -> `COMMAND_NOT_IMPLEMENTED` fence;
- profile mismatch is rejected before feedback-specific execution.

The complete deterministic suite remains green.

## Still outside this checkpoint

R8-022 does not claim parity for:

- feedback archive source enumeration;
- redaction-key lifecycle or redaction algorithm;
- cache creation/retention;
- diagnostics generation;
- file truncation/tail limits;
- rotated/segmented log semantics;
- pending/incomplete archive recovery;
- native save-dialog defaults/filter/path rules;
- archive metadata/signature/hash semantics;
- archive verification;
- exact canceled-result values;
- progress-event timing/byte accounting;
- `FEEDBACK_EXPORT_IN_PROGRESS` runtime reproduction;
- completed-archive opening behavior.

## Validation

Completed before packaging:

- Settings panel hash matches immutable 0.3.1;
- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed in this checkpoint.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
