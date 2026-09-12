# Update comparison and future automatic-update readiness

**Priority correction after review 9:** PM9-A and future automatic-update implementation are deferred until [the first real resource point demonstration](first-live-result.md). This document preserves the comparison and future requirements. Existing minimal current-client integrity/fingerprint checks still apply before live tests.

## LWB-PM9-001 — verified result, 2026-09-10

The **user performed the data deletion/reinstall**. The AI observed and checked the resulting files/logs. PM8-0 recovery is closed; do not repeat the destructive cleanup or describe it as an AI-delivered updater.

[Fresh comparison](../evidence/official-runtime/2026-09-10-pm9-update-comparison.json), [runtime snapshot](../evidence/official-runtime/2026-09-10-pm9-official-runtime.json) and [update-health snapshot](../evidence/official-runtime/2026-09-10-pm9-update-health.json) establish:

| Component | Before / after | Applicability |
|---|---|---|
| Main application | `1.0.361 / 1078`; launcher `0.1.2`, unchanged | A main-version check alone missed this update. |
| Sampled core files | All 14 previous/current SHA-256 anchors match, including Assembly-CSharp/xLua | Those exact static binary findings retain source identity. This is not complete installation equality or a runtime ABI guarantee. |
| Active Lua payload | Version 12 -> 14; 42,030,332 -> 41,269,242 bytes; SHA-256 `bc10cda45528ef82adb1439b0cac5b08265c7a11770430e7484a27a7031c485c` -> `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace` | Revalidate script-dependent contracts. Size delta is -761,090 bytes (after minus before), not a decoded feature count. |
| Data table | September 8 table `39128` -> `39191` before the failed update; `39191` remains unchanged through reinstall and this audit | Revalidate old table-dependent IDs/labels. Do not attribute that earlier table change to the reinstall. |
| Failed/successful path | The recorded `14 <- 12` patch failed repeatedly; reinstall restored built-in 11, then `14 <- 11` succeeded | The successful different base/path explains the recovery sequence, not the underlying defect mechanism. |
| Accepted update | Active size/CRC/version match metadata and applied-update record; failed temporary output is absent | Log order is failure line 39720, applied update 39869, game start 39874; Player log contains the recorded initialization signals. |

The preserved failed temporary file and accepted version-14 file have equal length but **36,528,505 differing bytes at corresponding offsets**. This read-only byte comparison does not decode either file, identify the CRC defect's cause or establish gameplay changes. Its hashes and method limits are in the comparison JSON. **Exact changed game behavior is UNKNOWN**; do not infer it from size, offsets or a patch filename.

Source locators and the cleanup/reinstall sequence remain in [the incident history](lastwar-update-incident.md). The fresh active script matches the already recorded PM8-004 successful snapshot. The unchanged core anchors preserve R6-048/050's current managed source identities, while the original LWBridge reference and its R6-049/051 builder findings are separate immutable evidence.

Reproduce with the existing read-only inspectors and **new dated output names**:

```powershell
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-10-pm9-official-runtime.json
python tools/inspect_lastwar_update_health.py --output evidence/official-runtime/2026-09-10-pm9-update-health.json
```

Compare named SHA-256/size/version fields in the JSON against the pre-failure and recovered snapshots. Raw-file CRC uses Python standard-library `zlib.crc32`; byte difference count is `sum(oldByte != newByte)` over paired offsets, with unpaired tail length reported separately. Do not overwrite historical snapshots on later runs. Full game files and raw personal logs remain outside Git.

## PM9-A — implement bounded update readiness, not an automatic reinstall

**Owner: standard AI. Status: DEFERRED until first live result, except an explicitly necessary prerequisite.** This is a new rebuild **IMPLEMENTATION POLICY**, not recovered original updater behavior. Its purpose is to stop stale contracts being used after an official update and provide a clear next action. It must not become a long diversion from PM7-B/C's connection and scan milestone.

The collector currently reports historical observations, not a complete readiness verdict. In particular, `latestGameStartFollowsLatestAppliedUpdate` only compares start and applied-update lines; it does not ensure a later failure is absent. Player-log substring presence alone does not correlate the log to that startup. Existing manual recovery evidence is valid for its identified sequence, but blindly combining those booleans into future automatic success would be wrong.

Required implementation and acceptance:

1. Build one reusable comparison/readiness service using existing diagnostics. Identify the main build **and** script version/hash, script metadata/CRC, selected table hash and relevant binary hashes. A missing/unstable/unreadable input is UNKNOWN, never unchanged or healthy. Compare against an explicitly identified last validated baseline; keep the newly observed candidate separate.
2. Separate update-file integrity, startup observation, contract compatibility and owned bridge readiness. A healthy official game does not make the rebuild's bridge ready. Expose concise existing status/error UI feedback without adding login or changing the recovered page layout unnecessarily.
3. Detect later failure after earlier success, pending output, metadata mismatch, files changing while read, missing logs and mismatched Player/launcher sessions. Compare ordered records within one identified log generation. Log rotation or uncertain correlation is UNKNOWN; do not rely solely on timestamps or old startup substrings. Use actual evidence to recover needed correlation markers; label any rebuild policy explicitly.
4. At app startup and before resuming affected game work after an observed update, refresh the snapshot. These trigger choices are implementation policy. Do not invent an original polling interval or retry count. Keep existing unsupported operations unavailable; do not use this service to manufacture bridge/session success.
5. Report changed components and dependent findings needing revalidation. Keep hash-identical static findings available, but invalidate affected current-runtime/session assumptions. Never silently promote an unknown/new script or table into the last validated baseline merely because downloading finished. Baseline acceptance requires documented checks for the affected contracts and fresh authoritative runtime evidence where applicable.
6. Leave downloading/patching to the official launcher until a supported update lifecycle is independently established. Do not automatically repeat the user's whole-data deletion/reinstall. Preserve settings/evidence, never promote a failing file, rewrite CRC/version metadata or bypass integrity checks. A recurring identical failure should stop automatic retries and report recovery required under a documented policy.
7. Tests must cover: unchanged full fingerprint; unchanged main version with changed scripts; changed table only; download with bad output CRC; success followed by later failure; stale/rotated Player log; partial/missing/unstable files; and the documented clean-base recovery sequence. Prove no game/cache/config file mutation, no automatic baseline promotion and no real game actions in deterministic tests. These are readiness tests, not full game acceptance.
8. Delivery: show the actual app/service call path and visible readiness result, or explicitly name the integration prerequisite still blocking it. Save a concise evidence record, update backlog/ledger and commit/push/verify. After this single checkpoint, return to PM7-B/C's supported connection -> smallest real scan -> displayed result target.

## Future update execution remains a separate open task

Before enabling unattended official updates, establish launcher ownership, game shutdown/inflight-job behavior, authoritative progress/completion/failure, supported recovery/resume, and compatibility revalidation. A user's successful manual reinstall is one observed recovery, not a universally safe automatic fallback. Exact Lua/table content diffs should be recovered from identified official artifacts when they materially affect a feature; do not decode unrelated content just to inflate a changelog.
