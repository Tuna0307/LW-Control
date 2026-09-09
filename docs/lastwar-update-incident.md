# Official launcher update incident — 2026-09-10

## Result

**The deterministic Lua patch failure was recovered through the official launcher's cleanup/reinstall path, and normal game startup is now verified.** `LWB-PM8-002` and `LWB-PM8-003` preserve the seven matching `14 <- 12` failures. `LWB-PM8-004` records the later official cleanup/reinstall beginning at 2026-09-10 01:13, successful Lua version 14 application at launcher-log line 39869, ordinary `LastWar.exe` start at line 39874, and a fresh `Player.log` that reaches Unity initialization, Application Awake, `GameEntry Init Begin/End`, and `XLuaManager:StartGame()`. The failed evidence remains preserved; no failed temporary output was promoted and no CRC/version metadata was edited by this recovery work.

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

## LWB-PM8-002 — deterministic retry and official recovery surface

**Date / scope:** 2026-09-10, PM8-0 official launcher recovery investigation. **Status:** RECOVERED static/log behavior plus current runtime reproduction; update recovery remains UNKNOWN/BLOCKED.

**Source identity:** installed `LastWarLauncher.exe`, SHA-256 `b6e29176f64c11a6f203d414fb50ee1e02293be318efda9c33cf221c83d763bc`; installed `LastWarSync.exe` (`Updater 0.1.0`), SHA-256 `18fde4ebe98767de1c769d2bcc474b726ad400a0d2c67178d7ded01aedfda3e9`; current `Launcher.log`; and the five incident files identified by `LWB-PM8-001`. Focused command/help/string evidence is preserved in [`2026-09-10-pm8-recovery-surface.txt`](../evidence/official-runtime/2026-09-10-pm8-recovery-surface.txt). The fresh retry snapshot is [`2026-09-10-pm8-retry-update-health.json`](../evidence/official-runtime/2026-09-10-pm8-retry-update-health.json).

**Exact locators / reproduction:** start the installed launcher normally, then rerun `python tools/inspect_lastwar_update_health.py --output evidence/official-runtime/2026-09-10-pm8-retry-update-health.json`. `Launcher.log` lines 39592-39593 record the 2026-09-10 00:19:41.426 patch request and 00:19:42.296 failure. Search the identified launcher binary for `Game integrity check found repair paths:` and `Super Cleanup`; run `LastWarSync.exe --root <installed-root> help` for its supported command surface. Historical `Launcher.log` lines 23687-23695 record a launcher-selected full Lua replacement on 2026-09-02 followed by normal game start.

**Result:** the fresh launcher start downloaded `LWScripts_14_12_u440.patch` again with advertised size `1,230,849` and CRC-32 `3874454969`, then produced `LWScripts.data.tmp` with the same raw CRC-32 `4055968188` and rejected it against expected `3541420783`. The same failure/output is now present across six recorded attempts from 2026-09-09 18:48 through 2026-09-10 00:19. This makes a transient one-download corruption unsupported by the current evidence. The launcher binary contains an official **Super Cleanup** recovery flow described as verifying/fetching resources and redownloading at least 3 GB; historical logs separately prove the launcher has selected a full `LWScripts_12_u440.bz2` replacement when the encryption format changed. `LastWarSync.exe` exposes only launcher `install`, launcher `update`, and broad `uninstall`; it has no script-only repair/verify command.

**Validation / limits:** the failed files were preserved again before the retry, with hashes matching `LWB-PM8-001`. Current free space was about 459 GB, so the launcher's stated 3 GB prerequisite is satisfied. The official Super Cleanup control was not executed: the configured Windows UI automation layer rejected both launch/control and window-state operations because its automatic review “couldn't determine the safety status of the request.” This is an environment restriction, not missing user authorization. No custom UI automation was used to bypass it, and no guessed cache deletion or manual full-package substitution was attempted. Therefore the exact cause of the publisher patch/output mismatch and the success of Super Cleanup for this incident remain UNKNOWN/BLOCKED.

**Implementation impact:** PM8-0 remains the first live-readiness gate. Normal `LastWar.exe` startup is not accepted because the ordinary launcher still stops at the Lua CRC failure. PM7-B/PM7-C may continue offline, but no live bridge/scan result can be promoted from this client state.

## LWB-PM8-003 — seventh deterministic retry remains failed

**Date / scope:** 2026-09-10, PM8-0 follow-up on the later launcher attempt that began at 00:31. **Status:** LIVE-OBSERVED failure / UNKNOWN-BLOCKED recovery.

**Source identity / locator:** installed `Launcher.log`, SHA-256 `b9a66e79cfa1cc38dad98df0842436ba30e8a6b0dc5fd9170ca16820fb9b6303`, plus the current five Lua update files recorded by [`2026-09-10-pm8-003-update-health.json`](../evidence/official-runtime/2026-09-10-pm8-003-update-health.json). Launcher-log line 39719 at `2026-09-10 00:31:20.696` advertises `LWScripts_14_12_u440.patch`, size `1230849`, CRC-32 `3874454969`; line 39720 at `00:31:21.582` rejects `LWScripts.data.tmp`, expected CRC-32 `3541420783`, got `4055968188`.

**Reproduction / result:** run `python tools/inspect_lastwar_update_health.py --output evidence/official-runtime/2026-09-10-pm8-003-update-health.json`. The active script still matches `42030332|3591022538` and version marker `12`; the downloaded patch still matches its advertised size/CRC; the pending output is still 41,269,242 bytes with raw CRC-32 `4055968188`, SHA-256 `c7d44497dc5debce1fd541901f7365aa527547b03faa6435d9b552c014b6506a`. No `Starting game at ...LastWar.exe` record follows this retry. No LastWar/launcher/sync process was running during the follow-up check.

**Validation / limits / impact at this checkpoint:** this seventh matching attempt strengthened the deterministic-failure diagnosis but did not identify the publisher-side patch/decoder cause or prove Super Cleanup success. The previously recorded UI-automation restriction prevented executing that official control through the available automation layer and was not bypassed. PM8-0 therefore remained OPEN at LWB-PM8-003; the later LWB-PM8-004 finding below supersedes that recovery status while preserving this historical restriction and failure evidence.

## LWB-PM8-004 — official cleanup/reinstall recovered the client and normal startup

**Date / scope:** 2026-09-10, PM8-0 recovery acceptance. **Status:** LIVE-PROVEN launcher update recovery and normal game startup.

**Source identity:** installed `LastWarLauncher.exe`, SHA-256 `b6e29176f64c11a6f203d414fb50ee1e02293be318efda9c33cf221c83d763bc`; launcher log SHA-256 `cc7d2ab008ab84f10ac652e570b7b56422e459e6eefcec80384fec5b60acd27f`; recovered active `LWScripts.data` SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`; `Player.log` SHA-256 `9f0798a4e6ee1850646ac2e1065273d48a579b33e69141a53c88fa9b58295bda`. Machine-readable evidence is [`2026-09-10-pm8-004-recovered-update-health.json`](../evidence/official-runtime/2026-09-10-pm8-004-recovered-update-health.json), [`2026-09-10-pm8-004-official-runtime.json`](../evidence/official-runtime/2026-09-10-pm8-004-official-runtime.json), and [`2026-09-10-pm8-004-runtime-comparison.json`](../evidence/official-runtime/2026-09-10-pm8-004-runtime-comparison.json).

**Exact locators / reproduction:** launcher-log line 39721 records `Cleaning cache files...`; the following installer lines remove the LocalLow game cache, temp/cache directories, installed `Game` directory and manifest as one launcher-controlled cleanup flow. Line 39857 records `Update completed successfully.` Line 39869 records `Applied LWLuaFile update` with version `14`, file version `3`, size `41,269,242`, CRC-32 `3541420783`. Line 39874 records ordinary startup of `Game/LastWar.exe`. Run `python tools/inspect_lastwar_update_health.py --output evidence/official-runtime/2026-09-10-pm8-004-recovered-update-health.json` and `python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-10-pm8-004-official-runtime.json`; compare their sampled hashes to the pre-recovery `2026-09-10-official-runtime.json` as captured in the comparison JSON.

**Result:** the launcher rebuilt the install from manifest version 0 to current build 1078, refreshed bundle/table/runtime content, copied built-in Lua version 11, downloaded `LWScripts_14_11_u440.patch`, and accepted the resulting version 14 payload. The active metadata is now `41269242|3541420783`, the active version marker is `14`, and both size and CRC match the latest applied update. `LWScripts.data.tmp` and the failed `14 <- 12` patch are absent. The fresh `Player.log`, created after the launcher start record, contains all five sanitized startup signals collected by the inspector: Unity engine initialization, Application Awake, `GameEntry Init Begin`, `GameEntry Init End`, and the `XLuaManager:StartGame()` call.

**Validation / limits:** all 14 sampled PE/container/protected-runtime anchors are byte-identical to the pre-recovery build-1078 snapshot, including `Assembly-CSharp.rdl` and original xLua. The table remains `table_39191_7476c6ef71b756a1033c7c79a5d0c177.data` with the same hash. The Lua payload is intentionally different because version 14 replaced version 12. The exact root cause of the failed `14 <- 12` patch remains UNKNOWN; the successful clean-base `14 <- 11` path proves recovery, not the defect mechanism. The prior UI-automation denial remains a historical environment restriction; the later successful launcher-controlled cleanup was observed from the resulting logs/files rather than automated through the denied control.

**Implementation impact:** PM8-0 is complete. The current official client is again a valid live-validation target for PM7-B/PM7-C. This does not prove the rebuilt app's launch bootstrap, bridge handshake/readiness, scan ingestion, or live Map Data output; those gates remain separate.

## PM8-0 — recovered baseline

Owner: regular AI. Status: **COMPLETE through LWB-PM8-004**. The official launcher completed a supported cleanup/reinstall, applied Lua version 14, started the ordinary game, and produced a fresh initialized `Player.log`. Preserve the failure history and recovered baseline when later live work changes client files.

1. Recheck processes and incident files before acting. Preserve any still-present failed temporary output, patch and metadata in an excluded local evidence/backup directory with hashes; do not commit bulk script files or raw personal logs. The snapshot already records their identities.
2. Inspect the actual official launcher recovery options and relevant current log events. Use a supported retry/repair/redownload path if available, then compare the new output and log result. Do not invent a repair menu or assume deleting one temporary file will fix it. No unlimited identical retry loop.
3. Do not manually promote the failed temporary file, edit the expected CRC/version marker, bypass integrity validation, or overwrite the intact active base with guessed content. Diagnose any proposed cache reset first and preserve exact restorable targets; do not wipe the whole profile/game to resolve this single error.
4. If the same verified patch repeatedly produces the same failing output, record reproduction and remaining official recovery/support options rather than declare repair. This environmental issue is not a Daybreak binary-analysis assignment by itself.
5. Acceptance is now satisfied by LWB-PM8-004: the official launcher completed its update and started the ordinary game without the CRC error, with current correlated launcher/Player logs and refreshed runtime/script/table fingerprints.
6. Revalidate only affected current-client findings. Unchanged Assembly-CSharp/xLua hashes preserve their static source identity, while new script/table contents require refreshed applicability. The immutable LWBridge 0.3.1 reference findings remain independent. No new LWBridge feature is LIVE-PROVEN by fixing the official launcher.
