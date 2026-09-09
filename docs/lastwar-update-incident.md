# Official launcher update incident — 2026-09-10

## Result

**The official launcher failed while producing the updated Lua script file. The download itself matches the launcher advertisement; the old active script is intact.** The exact fault is not yet established. The game has not been repaired or proven to launch by this audit. No installed game, launcher, script, metadata or configuration file was changed. The five script/update files were also copied to ignored local `.codex-live/pm-review-8/failed-update-backup/` and verified against the recorded hashes; no script binaries are committed.

This is **LWB-PM8-001**, an observed local update failure with reproducible file/log evidence, separate from LWBridge reconstruction and from its encrypted bridge package. Do not conflate the official `LWScripts.data.tmp` with LWBridge `bridge-scripts.dat` or `package-key.envelope`.

## Evidence and source identity

The user screenshot shows `LWLua decode failed: crc mismatch` for `lwScripts/LWScripts.data.tmp`, expected `3541420783`, got `4055968188`. The matching source is installed `Launcher.log`, line **39211**, timestamp **2026-09-10 00:01:57.986** as logged. The immediately preceding line **39210** advertises the patch. Log SHA-256 and file identities are in the [update-health snapshot](../evidence/official-runtime/2026-09-10-lua-update-health.json). Snapshot timestamps use UTC; the incident date is Singapore local time.

| File / observation | Measured result |
|---|---|
| `LWScripts_14_12_u440.patch` | 1,230,849 bytes; raw CRC-32 `3874454969`, both equal the advertised values. SHA-256 `8bdd05c426597a99d54429e2c622e15a8c43c349d628d9051aec7193fe2ece19`. |
| Old active `LWScripts.data` | 42,030,332 bytes; raw CRC-32 `3591022538`, both equal `LWScripts.txt`. SHA-256 `bc10cda45528ef82adb1439b0cac5b08265c7a11770430e7484a27a7031c485c`, unchanged from September 8. Active version marker remains `12`. |
| Failed `LWScripts.data.tmp` | 41,269,242 bytes; measured raw CRC-32 `4055968188` equals the failure message, not expected `3541420783`. SHA-256 `c7d44497dc5debce1fd541901f7365aa527547b03faa6435d9b552c014b6506a`. |
| Main install | Version `1.0.361`, build `1078`, launcher `0.1.2` still reported. All 14 sampled PE/container/protected-runtime SHA-256 anchors match the previous snapshot, including Assembly-CSharp and xLua. |
| Other hot-update data | Latest table changed from `table_39128...` to `table_39191...`; refresh table-dependent facts before reuse. This is not a claim every other runtime file was checked. |

The [baseline comparison](../evidence/official-runtime/2026-09-10-runtime-baseline-comparison.json) links both full runtime snapshots and all sampled hashes. Game/launcher processes were absent during deterministic checks. There is no fresh successful launch evidence after this failure.

## Interpretation and limits

The downloaded patch is consistent with the recorded advertisement, so a simple incomplete/corrupted download is not supported by these measurements. The stored active base also matches its old metadata and previous hash. The reconstructed output is what fails integrity validation. A publisher patch/manifest problem, launcher processing issue or another input dependency remains possible; none is proven. Do not blame the rebuild, the user's machine or the publisher without further evidence. Matching hashes of sampled files are not a full forensic exclusion of every possible cause.

Do not mark the update successful because `manifestAppVersion` remains 1078 or because the old architecture inspector reports `latestLuaState` version 12. That field is the last historical matching log event and ignores this newer failure. Successful PE/version checks likewise do not validate patch output or gameplay.

## Reproduction

Use Python 3 with standard-library hashlib/zlib/pathlib; no new package installation is needed. Run from the repository root. Use new output names for later observations so historical evidence is preserved.

```powershell
python tools/inspect_lastwar_update_health.py --output evidence/official-runtime/2026-09-10-lua-update-health.json
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-10-official-runtime.json
```

The first inspector records file hashes, raw CRC-32, sizes, read stability and only structured relevant log fields. It does not decode/apply patches, expose user identifiers or modify game files. The second records version/architecture anchors and historical log summaries; always read it alongside update health. Each report's existence/return code means collection succeeded, not that the update passed.

## PM8-0 — next task: restore a valid official update before live acceptance

Owner: regular AI. Status: **OPEN, diagnosis complete / recovery unproven**. Priority: first before any live scan/launch acceptance. Independent offline work may continue.

1. Recheck processes and incident files before acting. Preserve any still-present failed temporary output, patch and metadata in an excluded local evidence/backup directory with hashes; do not commit bulk script files or raw personal logs. The snapshot already records their identities.
2. Inspect the actual official launcher recovery options and relevant current log events. Use a supported retry/repair/redownload path if available, then compare the new output and log result. Do not invent a repair menu or assume deleting one temporary file will fix it. No unlimited identical retry loop.
3. Do not manually promote the failed temporary file, edit the expected CRC/version marker, bypass integrity validation, or overwrite the intact active base with guessed content. Diagnose any proposed cache reset first and preserve exact restorable targets; do not wipe the whole profile/game to resolve this single error.
4. If the same verified patch repeatedly produces the same failing output, record reproduction and remaining official recovery/support options rather than declare repair. This environmental issue is not a Daybreak binary-analysis assignment by itself.
5. Acceptance: the official launcher completes its update and starts the ordinary game without this error, confirmed by current correlated logs/observation. Then save a new dated runtime/script/table fingerprint and compare with the previous successful/failed baseline. A launcher process alone is insufficient.
6. Revalidate only affected current-client findings. Unchanged Assembly-CSharp/xLua hashes preserve their static source identity, while new script/table contents require refreshed applicability. The immutable LWBridge 0.3.1 reference findings remain independent. No new LWBridge feature is LIVE-PROVEN by fixing the official launcher.
