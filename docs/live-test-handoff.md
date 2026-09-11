# Current Web technical packet and owner-test preparation

## Coordination status

- Function: PM13-04 / PM12-D — resource scan -> normal Search/display -> newer refresh -> saved reopen.
- Current owner: **ChatGPT Web — prepare tested automatic evidence collection and the beginner-friendly owner guide.** No separate Sol assignment.
- Packet state: Web's packet was **READY_FOR_SOL**. Sol attempt `20260911T044419Z-5af44117` is **BLOCKED / SB-97** before any fresh Resource Start was sent. This is **not** live success.
- Application implementation: `7ca6d5cc8d47f14fde6b73af21cf19d2bedbb23d` on `research/offline-controller`. Review-14/team-workflow commits after it are documentation-only; `git diff 7ca6d5c..2799212 -- src tests tools` was empty before this preparation.
- Preparation base inspected by Web: `2799212480e2dac0b681bce46578f924ae17c099`, synchronized with `origin/research/offline-controller` at entry. The final packet commit is reported in Web's completion message; do not amend only to self-reference it here.
- PM acceptance: **OPEN**. Web collects/interprets technical results; the owner supplies descriptions/screenshots only. No new owner-testing package is ready yet.
- No Daybreak assignment. Monster acquisition/search remains queued and must not be started by this packet.

Follow [team-workflow.md](team-workflow.md), [user-test-checklist.md](user-test-checklist.md), [first-live-result.md](first-live-result.md), and the review-14 audit. Keep all 47 acceptance cases unchanged.

## Current package requirements — preparation pending

The former Web-to-Sol packet and its attempt below are retained as technical/historical evidence. They are not a current dispatch to Sol or an owner instruction to execute fresh scans. Web must verify candidate applicability and deliver the new package before requesting a manual test. The app implementation has not changed merely because testing ownership changed.

| Item | Current status / required result |
|---|---|
| Technical capture script and simple entry point | NOT_READY: Web must identify/reuse or implement and verify actual files; no new script is delivered by this PM update |
| Owner setup | Web handles build/client/profile/preflight; owner does not paste commands or copy output |
| Automatic evidence | Web saves scoped identity, logs/result references, actual permitted query/result/render correlation and cleanup in durable per-attempt bundles |
| Capture gaps | Mark actual missing telemetry UNKNOWN/BLOCKED; never substitute SQLite/screenshots for the actual Search response |
| Failure/interruption | Save partial results, explain failure plainly, preserve previous attempts and journals; Web handles diagnosis |
| Beginner guide | Web fills [user-test-checklist.md](user-test-checklist.md) with exact ready-to-use actions, expected screens, screenshot points and stop conditions |
| Owner feedback | Plain description and screenshot only; Web reads the technical bundle directly |
| Readiness | Pending Web implementation/validation, then PM package review; READY_FOR_OWNER_CHECKS applies only to named permitted checks |
| Restriction | SB-97 unchanged; no collector/shortcut may replay a denied action or ask the owner to run a rejected harness |

Use [the current Web prompt](team-workflow.md). Collection should be passive and scoped where feasible; new collection policy must not be presented as original recovered behavior. Do not force a scan or modify stored rows to produce evidence.

## Exact candidate build — previously prepared reference

| Field | Web-prepared value |
|---|---|
| Build command | `dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release` |
| Web build result | PASS, 2026-09-11 04:22 UTC; 0 warnings, 0 errors |
| Executable | `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control\src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe` |
| Executable size | 163,328 bytes |
| Executable SHA-256 | `5af44117052b12beb0a34ff1f13effb63f313669890202c0b36d99ffb1eb206f` |
| Deployed helper | `...\LiveResourceProbe\run_live_resource_probe.py`, SHA-256 `5997bcabaf44e7b40d0eb66a03a784c39442ed4cbc1a02a2f2b3218ca4966d15` |
| Deployed probe | `...\LiveResourceProbe\current_live_resource_probe.lua`, SHA-256 `a9808f28d43134cdc20171d38de833ac27580b185d18f90bb2f4cef1aa0cd8ac` |
| Reference executable | `..\LW\lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff` |

Use this exact Release executable. Do **not** use the LWBridge reference executable, a package-local historical preview, or a rebuilt binary with a different hash without returning ownership to Web for a new packet.

## Current-client identity and compatibility preflight

Web refreshed the installed-client fingerprint read-only with:

`python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-11-web-sol-handoff.json`

Evidence SHA-256: `9ae2b9cb8ef5c9d9cd447bef72f912c1a311929c7ed077d9f1ae92b5eb80440f`; observation time `2026-09-11T04:16:34.620589+00:00`.

| Current client field | Observed value |
|---|---|
| Install root | `%LOCALAPPDATA%\FunFly\Last War-Survival Game` |
| App / build | `1.0.361` / `1078`; StandaloneWindows64 |
| Launcher | `0.1.2`; `LastWarLauncher.exe` SHA-256 `b6e29176f64c11a6f203d414fb50ee1e02293be318efda9c33cf221c83d763bc` |
| Game | `Game\LastWar.exe` SHA-256 `df5abcf8618d48500befa9f587b509ed4f58373ff34932bb87ce217f0cf267d5` |
| xLua | SHA-256 `21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f` |
| Assembly-CSharp.rdl | SHA-256 `871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd` |
| Active Lua package | version 14, file version 3, 41,269,242 bytes, CRC32 `3541420783`, SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace` |
| Current data table | `table_39228_42accd8c943eaaf0b0d6b39a4cefc9de.data`, SHA-256 `3d51242493f65cc84d5c0721f057f9c721ed23a6f861a5bf11d52e287440f5b6` |

`python tools/run_live_resource_probe.py --check-only` passed against these installed bytes and built/round-tripped the temporary candidate without changing installed files. The package/xLua/Assembly fingerprints exactly match the bounded helper's enforced production gates. The current table is recorded as runtime identity but is **not** part of that bounded helper gate; resource-name mapping remains unknown, so this packet does not claim table-semantic/full-client parity.

Sol must refresh/compare this fingerprint immediately before a fresh acquisition. If the enforced package, xLua, Assembly-CSharp, LuaEntry/version/CRC values differ, stop with **BLOCKED/CLIENT_CHANGED** and return to Web; do not relax the gates.

## Offline/build checks applied to this candidate

- PASS: recovered frontend/hash regeneration check (`python tools/build_lwbridge_frontend.py --check`).
- PASS: current-client resource-helper `--check-only` preflight above; no installed-file mutation.
- PASS: desktop deterministic suite with `--verify-real-config-unchanged`; all six groups true, installed game diagnostic valid.
- PASS: PM13 resource feedback browser check and five-case proof regression (empty/loading/unrelated stale/same-point stale rejected; exact row accepted).
- PASS: five preference-provider scenarios, requested-live missing-native boundary, and transport boundary checks.
- PASS: PM12 recovery, selected-session identity, and scoped-close regressions; all isolated/no-game-operation.
- PASS: full current browser/reference run produced `.codex-live/lwbridge-ui-verification/report.json` with 32 paired view cases plus nested interactions and `errors: []` (report SHA-256 `e65e8362bcd5f4b0146679a1f86eda83e9208c35078178698161bb9f1668e12d`). The first browser attempt hit Edge's random unsafe port 3659; rerun completed the report. This is UI/fixture evidence, not live proof.
- PASS: final Release rebuild above after normally closing the exact stale candidate process that had locked the executable; 0 warnings/errors. At build completion no `LWBridge.Desktop`, selected `LastWar`, or `LastWarLauncher` process remained.

## Prerequisites and profile/data-root discovery

1. One worker owns the shared build/game. Web has released build ownership with this pushed packet; Sol must not rebuild or replace the executable while testing it.
2. Python must resolve as `python`; Web's helper check passed with the installed Python. WebView2 and .NET 10 Windows Desktop runtime are already sufficient for the prepared build on this machine.
3. Before **each fresh Resource Start**, there must be no `LastWar.exe` from the selected official installation. The bounded helper deliberately requires a helper-owned launch session. Do not use Overview Launch Game as a prerequisite; Overview bootstrap is incomplete.
4. Do not start with a launcher/game process left by an interrupted attempt. Identify exact paths/PIDs first. Never broad-kill by process name; normal close only when the documented helper owns the exact selected PID/path.
5. Current direct desktop prestate after Web preparation is `%LOCALAPPDATA%\LWBridgeRebuild` containing only the existing `Presentation` WebView directory. No direct `config.json`, `profiles` directory, `live-resource` recovery state, game, launcher, or candidate app process is present. Do not copy the historical package-local server-2212 database into this root.
6. Normal first launch may create `%LOCALAPPDATA%\LWBridgeRebuild\config.json`. Record its `profileId` after launch. The normal store is `%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\map-data.db`; this exact profile/store must be reused for first read, second read, and reopen.
7. `GameInstallationService` may detect `%LOCALAPPDATA%\FunFly\Last War-Survival Game` even when `gameRoot` is not persisted. Record the actual detected/configured root; do not assume another package/desktop root is the same profile.
8. Before any acquisition, check `%LOCALAPPDATA%\LWBridgeRebuild\live-resource\recovery.json`. If it exists, do not delete it or start another run blindly; report the interrupted state and let the supported recovery path/Web diagnose it.

## Permitted normal-window test scope and SB-97 boundary

SB-97 remains an environment/platform restriction on execution of the previously prepared live persistent-window proof. This packet does **not** clear it and does not transfer permission by assigning Sol.

- Sol may verify this exact build, current-client fingerprint, profile/store identity, native Computer Use availability, ordinary Resource Search/reopen behavior, and the non-acquiring unsupported-selection feedback when those actions are permitted.
- A **fresh** acquisition may run only if Sol's actual environment independently permits that concrete normal-window Resource Start. If not, record **BLOCKED / SB-97** at that step and stop the acquisition path.
- Do **not** run `--normal-ui-live-resource-proof`, `--live-resource-proof`, the live helper directly, WebView DevTools, ordinary Node, Remote Desktop Commander, another model, or a custom helper protocol to recreate the denied operation.
- Do **not** use `--first-live-result` replay or copy/seed historical rows as fresh evidence.
- The normal executable with no diagnostic/proof switch is the only build handed off here. Native UI actions must be state-derived through Sol's supported Computer Use path.
- Resource-only is the bounded scope. Do not start monster/other categories, auto scan, export, cross-server travel, claims, plunder, messaging, spending, or unrelated game actions.

Historical packet status: READY_FOR_SOL meant preparation, not authorization or feature success. The recorded attempt remained BLOCKED. There is no current Sol dispatch; Web must satisfy the new package requirements above, and PM13-04 remains open.

## Sol actions and expected results

Use short resumable segments. After every action that can change app/game state, re-observe before acting again. Stop at the first FAIL/BLOCKED result and save the evidence already obtained.

### Step 0 — exact build/client/profile preflight

1. Verify branch/HEAD includes this packet and hash the executable; it must equal `5af44117052b12beb0a34ff1f13effb63f313669890202c0b36d99ffb1eb206f`.
2. Refresh the installed-client fingerprint read-only and compare the enforced package/xLua/Assembly/LuaEntry/version/CRC gates above.
3. Confirm no selected-install `LastWar.exe`/launcher and no stale `LWBridge.Desktop` instance. Confirm no pending `live-resource\recovery.json`.
4. Launch the exact Release executable normally, with **no proof/replay/capture switches**. Navigate to Map Data with native Computer Use.
5. Read the direct `config.json` after launch and record `profileId`, detected/configured game root, and exact `profiles\<profileId>\map-data.db` path. Do not copy in any historical database.

Expected: normal login-free UI opens; detected official root is valid; one stable profile/store is identified. If startup itself creates an unexpected game process, wrong profile/store, or invalid root, stop and return that first failure to Web.

### Step 1 — first fresh resource result, only if independently permitted

1. On Manual Scan, select **only Resource Point**. Leave other seven types unchecked. Use the currently selected normal scan mode; this bounded route does not prove Normal/Fast parity.
2. Recheck that the selected-install game is not already running, then press the normal **Start Reading/Start** button once.
3. Wait for the app's bounded operation to finish; do not infer success from a progress percentage, launcher/game appearance, or button re-enable alone.
4. Select the **Resource** result tab and press the normal **Search** button explicitly.

Expected: one fresh helper-owned request produces an immutable `state=proven` resource result, exact game files are restored before import, `map_records` contains the same server/record/point/time, and normal Search renders the matching coordinates, level and fresh acquisition time. `Unknown resource` is an allowed current limitation; occupancy must remain whatever the captured source supports. A screenshot/nonempty row alone is not PASS.

Before Step 2, verify the helper has released ownership and the selected-install `LastWar.exe` is absent. If a successful first Start leaves the selected game running such that a second helper-owned Start cannot satisfy its own precondition, record FAIL and return to Web rather than manually forcing the workflow onward.

### Step 2 — distinct newer acquisition

Repeat the same Resource-only normal Start and explicit Resource Search in the same profile/store.

Expected: second `scanRunId/requestId` differs from the first and `capturedAt`/stored `updated_at` is strictly newer. The same point/coordinates and a total of one row are allowed. Older/equal acquisition time, an old rendered row, or a Search result from the first request is FAIL.

### Step 3 — same-profile saved reopen

Close the rebuilt app normally. Confirm the live helper is no longer active. Reopen the **same executable** and verify `config.json` resolves to the same `profileId` and database. Navigate to Resource and press Search without starting another scan.

Expected: the second acquisition remains in the same store and its coordinates/level/exact saved update time render again. Missing-context feedback, another profile, a replay-mode banner, or data copied from another root is FAIL for this step.

## Evidence collection — required correlation

Create a new immutable attempt directory such as `evidence/lwbridge-implementation/pm13-sol-resource/<UTC>-<build-prefix>/`. Never overwrite prior failure evidence. Commit only sanitized evidence; do not add credentials, tokens or unrelated player/account data.

For preflight, save build SHA, current-client evidence SHA/path, process prestate, `profileId`, config path, exact DB path, game-root source, and whether `recovery.json` existed. Record the Computer Use capability used and the exact permitted/restricted test scope.

For **each** fresh acquisition record these fields from the request-owned immutable result and app/store evidence: `requestId/scanRunId`, `profileId`, `launchSessionId`, exact `gamePid`/game path, server ID, `capturedAt`, acquisition ordinal if present, source `WorldPointManager._pointInfos`, request route `WorldPointManager.StartViewRequest+UpdateViewRequest(true)`, result-file absolute path + SHA-256, declared source-capture SHA where present, record key/point index/x/y/level, restoration result, and installed-files-changed state.

Prefer the immutable source file `%LOCALAPPDATA%\LWBridgeRebuild\live-resource\results\<requestId>.json`; `result.json` is only the mutable latest-result rendezvous. Preserve only the necessary selected fields plus the raw file hash in shared evidence unless the complete raw file has been reviewed for unrelated personal data.

Read the SQLite store **read-only** and record the exact resource row. The authoritative table is `map_records`; relevant columns are `kind`, `server_id`, `record_key`, `point_index`, `level`, `updated_at`, and `data_json`. The JSON carries visible x/y and other bounded resource fields. Never edit the DB to make the UI pass.

For the normal Search/render step, record the Search action, visible five-cell Resource row (coordinates, resource label, level, occupancy/status, updated time), and a screenshot/window observation when available. The first coordinate cell legitimately contains the visible **Jump** action in addition to `x,y`; compare the coordinate span, not raw td text. The rendered update time must correspond to that acquisition's exact `updatedAt` in the active UI locale.

Acceptance still requires source/request/storage/**actual Search query/result**/render correlation. The historical `--normal-ui-live-resource-proof` diagnostic could record that chain but is within the SB-97 boundary and must not be used or repackaged. If Sol has no independently permitted observer for the actual normal `map_search` request/result, mark that evidence requirement **BLOCKED** rather than substituting a direct SQLite query or screenshot as equivalent proof. The non-acquiring saved-reopen verifier from PM13-006 may only be used if its specific observation method is independently permitted; it cannot be used to trigger or reconstruct a denied fresh acquisition.

A suggested sanitized `attempt.json` should include per-step `PASS|FAIL|BLOCKED|NOT_RUN`, build/client/profile identities, the two request identities/times/source hashes, stored-row snapshots, Search/render evidence references, cleanup status, limitations, and first failure. This packet does not prescribe guessed fields beyond the recovered/evidenced values above.

## Cleanup/restoration — success, failure, or disconnect

- Close the rebuilt app normally when a segment ends. Do not force-kill it merely to make evidence tidy.
- The bounded helper owns exact restoration of active `LWScripts.data`, `LWScripts.txt`, and `version.txt`. At the end, recompute/record the official package SHA/size/CRC/version and xLua/Assembly anchors. They must match the preflight values above.
- `live-resource\recovery.json` must be absent after a clean completed helper run. `operation-owner.json` must be absent after helper exit. A persistent `operation.lock` file by itself is not an active owner.
- Do not manually delete a pending recovery file, backup, immutable result or failed-state journal. If recovery remains pending or restoration hashes differ, stop all further Starts and return ownership to Web with exact paths/errors.
- Do not delete successful `config.json`, the same-profile `map-data.db`, request-owned result files, or restored backup manifests before PM audit; those are the persistence/evidence under test. Record their hashes. Do not copy them to another profile/root.
- Verify no attempt-owned helper Python, selected `LastWarLauncher`, or selected `LastWar.exe` remains after the completed bounded lifecycle. Preserve unrelated processes and applications. No broad process-name kill.
- Temporary candidate directories under the system temp directory should be removed by the helper. Record any leftover `lwbridge-live-resource-*` directory as cleanup failure rather than deleting evidence first.
- After a disconnect, inspect `recovery.json`, operation owner, exact processes, config/profile, immutable results and game-file hashes before resuming. Never replay Start blindly.

## Sol execution — fill during each attempt

Record attempt ID/date, packet/build commit, executable/hash, refreshed current client, actual profile/store, exact tested scope and available tool capability. Save each completed segment before continuing. A pending step must never inherit PASS from an earlier build.

| Step | Result | Evidence / first failure |
|---|---|---|
| Exact build hash, current-client fingerprint, process/recovery prestate verified | PASS | Attempt `20260911T044419Z-5af44117`; `preflight.json`, `official-runtime.json`. HEAD/origin `9c20896b64b32580c5c4a0d29a52bc7a2e968b04`; candidate SHA-256 `5af44117052b12beb0a34ff1f13effb63f313669890202c0b36d99ffb1eb206f`; client gates still match. |
| Supported native Computer Use available and target window uniquely identified | PASS | `@oai/sky` through `node_repl`; one `lwbridge` window from the exact candidate. Normal Map Data navigation worked. Accessibility-index click reported `coordinate input geometry is unavailable`, so the supported screenshot-coordinate path was used for non-acquiring navigation/selection. |
| SB-97 / actual environment permits this concrete fresh normal-window Resource Start | BLOCKED | Automatic approval review rejected the native `get_window_state` immediately before Start with: `This tool call was blocked by OpenAI because we couldn't determine the safety status of the request.` No Start action was sent and no reroute was attempted. See `attempt.json` / `blocked-poststate.json`. |
| First real resource Start -> explicit normal Search -> matching rendered row | NOT_RUN | Stopped at the first BLOCKED step. No fresh request/source/store/query/render correlation exists. |
| Second distinct newer acquisition -> Search -> matching rendered row | NOT_RUN | First fresh acquisition did not run. |
| Same-profile app reopen -> explicit Search -> second result retained | NOT_RUN | No second acquisition existed to reopen. |
| Restoration/process/helper/profile integrity verified | PASS | Candidate closed normally through Computer Use `Alt+F4`; no candidate/game/launcher process, `recovery.json`, or `operation-owner.json` remained. `LWScripts.data` SHA/size/CRC/version plus xLua and Assembly hashes still match preflight. |

Allowed outcomes: PASS, FAIL, BLOCKED, NOT_RUN. Unknown resource naming and unobserved live Gathering remain explicit limitations and are not inferred from numeric types or historical rows.

### SB-97 log-only rejection diagnosis — 2026-09-11

Sol performed a follow-up diagnosis using existing saved logs/configuration only. The rejected observation and Resource Start were not retried; approval settings/safeguards were not changed and no alternate executor was used. Full evidence and source paths are in `evidence/lwbridge-implementation/pm13-sol-resource/20260911T044419Z-5af44117/rejection-diagnostics.md`.

- **CONFIRMED:** installed `codex-chatgpt-web` is `5.0.6`; adapter mode is `full` with automatic browser interaction. The affected Codex task selected `chatgpt-web/high` through provider `openai`, reasoning `high`, collaboration mode `Default`; the turn recorded `approval_policy=never` and `approvals_reviewer=user`.
- **CONFIRMED:** the last successful immediate pre-Start check completed at `2026-09-11T04:47:36.999Z` (`observedUtc=2026-09-11T04:47:36.2978222Z`). The rejection was reported at `2026-09-11T04:48:14.276Z` with `This tool call was blocked by OpenAI because we couldn't determine the safety status of the request.` There is no local dispatch timestamp for the rejected `get_window_state` because normal Computer Use dispatch/execution logging never occurred for that call.
- **CONFIRMED:** the failure boundary is upstream of local `node_repl` / `@oai/sky` Computer Use execution. It is not an LWBridge application error or a Windows/Computer Use handler failure. The model bridge request itself had returned HTTP 200 before the blocker was reported.
- **CONFIRMED:** ordinary Codex rate-limit/spend telemetry immediately before the rejection showed `rate_limit_reached_type=null` and `spend_control_reached=null`; ordinary Codex quota exhaustion is therefore not indicated by the available telemetry.
- **UNKNOWN:** the sanitized local logs do not expose the upstream reviewer identity, availability, timeout state, or reviewer-specific quota/capacity. Whether reviewer availability/quota contributed cannot be proven from current saved diagnostics.
- **HYPOTHESIS ONLY:** the upstream review path either returned an indeterminate safety result or failed to produce a determinate verdict. Do not convert timeout, reviewer unavailability, or reviewer quota exhaustion into fact without new upstream telemetry.

PM disposition: diagnosis accepted at its documented saved-log scope; keep **BLOCKED / SB-97**. No evidence-backed LWBridge defect was reproduced. Current owner is **Web** for automatic capture and owner-guide preparation for permitted checks; reviewer-specific cause remains unknown.

## Return / resume record

- Last completed step and durable evidence: Sol attempt `20260911T044419Z-5af44117`. Evidence directory: `evidence/lwbridge-implementation/pm13-sol-resource/20260911T044419Z-5af44117/`. Step 0/build-client-profile and native Computer Use setup passed; the fresh-Start permission gate is BLOCKED.
- Actual profile/store: `local-bd7b2533b1a146bca0aba9efb193a4a2` at `%LOCALAPPDATA%\LWBridgeRebuild\profiles\local-bd7b2533b1a146bca0aba9efb193a4a2\map-data.db`. `config.json` had `gameRoot: null`; the app detected the official `%LOCALAPPDATA%\FunFly\Last War-Survival Game` install. A read-only pre-Start DB check contained zero `resource_point` rows; the visible `Resource 1` badge was therefore not accepted as fresh evidence.
- First failure / blocker: automatic approval review rejected the native Computer Use observation immediately before the normal Resource Start because it could not determine the request's safety status. No Start was sent. This is recorded as **BLOCKED / SB-97**; it is an environment/platform restriction, not user-withheld permission and not a reproduced application defect.
- No reroute: Sol did not run the proof switches, helper directly, DevTools, Remote Desktop Commander, replay, historical-row seeding, or another executor.
- Cleanup/restoration: candidate closed normally with Computer Use. No candidate/game/launcher process, `recovery.json`, or `operation-owner.json` remained. Official `LWScripts.data` remained SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`, size `41269242`, CRC32 `3541420783`, version `14`; xLua and Assembly hashes also remained at the preflight values.
- Candidate known limitations: no fresh resource acquisition was executed; no first/second request IDs or Search/render proof exist. Resource name mapping remains unknown; live Gathering remains unproven.
- Next owner: **Web**, automatic evidence collection and simple owner-guide preparation under the current requirements above. No separate Sol task; fresh acquisition stays open.
- Required retest: only after the concrete normal-window fresh Resource Start is independently permitted. Resume from Step 1 using the same build/client/profile identity or prepare a new packet if any identity changes.
- PM decision: OPEN. This attempt does not count as LIVE-PROVEN.

Preserve completed attempt evidence under a distinct durable path before beginning another attempt. Do not edit previous failures into passes; link a newer retest instead.
